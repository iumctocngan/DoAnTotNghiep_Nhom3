using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace CmsEdu.Api.Services;

public sealed class Module3AiClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<Module3AiClient> logger) : IModule3AiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<JsonElement> GetStudentContextAsync(int studentId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"api/ai/cskh/student-context/{studentId}");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        try
        {
            var context = await response.Content.ReadFromJsonAsync<JsonElement>(
                JsonOptions,
                cancellationToken);
            if (context.ValueKind != JsonValueKind.Object)
                throw new JsonException("Student context must be a JSON object.");
            return context.Clone();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Module 3 AI Service returned an invalid student context.");
            throw new Module3AiException(502, "AI Service trả về dữ liệu context không hợp lệ.");
        }
    }

    public async Task<AiCallScriptResponse> GenerateCallScriptAsync(
        int studentId,
        string question,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            "api/ai/cskh/call-script",
            JsonContent.Create(new GenerateCallScriptRequest(studentId, question), options: JsonOptions));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        try
        {
            var answer = await response.Content.ReadFromJsonAsync<AiCallScriptResponse>(
                JsonOptions,
                cancellationToken);
            if (answer is null ||
                string.IsNullOrWhiteSpace(answer.Summary) ||
                string.IsNullOrWhiteSpace(answer.RiskAnalysis) ||
                string.IsNullOrWhiteSpace(answer.ActionPlan) ||
                answer.SuggestedCallScript is null ||
                answer.SuggestedCallScript.Count != 5 ||
                answer.SuggestedCallScript.Any(string.IsNullOrWhiteSpace))
            {
                throw new JsonException("AI Service returned an incomplete call-script response.");
            }

            return answer;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Module 3 AI Service returned an invalid call-script response.");
            throw new Module3AiException(502, "AI Service trả về nội dung kịch bản không hợp lệ.");
        }
    }

    public async Task<PredictionResultResponse> PredictStudentAsync(
        int studentId,
        int enrollmentId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            "api/ml/predict-student",
            JsonContent.Create(new PredictStudentRequest(studentId, enrollmentId), options: JsonOptions));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        try
        {
            var prediction = await response.Content.ReadFromJsonAsync<PredictionResultResponse>(
                JsonOptions,
                cancellationToken);
            if (!IsValidPrediction(prediction) ||
                prediction!.StudentId != studentId ||
                prediction.EnrollmentId != enrollmentId)
            {
                throw new JsonException("Module 3 AI Service returned an incomplete or mismatched prediction.");
            }

            return prediction;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Module 3 AI Service returned an invalid student prediction.");
            throw new Module3AiException(502, "AI Service trả về kết quả dự đoán không hợp lệ.");
        }
    }

    public async Task<BatchPredictionResponse> PredictBatchAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/ml/predict-batch");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        try
        {
            var predictions = await response.Content.ReadFromJsonAsync<BatchPredictionResponse>(
                JsonOptions,
                cancellationToken);
            if (predictions is null ||
                predictions.Results is null ||
                predictions.TotalProcessed < 0 ||
                predictions.Results.Count != predictions.TotalProcessed ||
                predictions.HighRiskCount < 0 ||
                predictions.MediumRiskCount < 0 ||
                predictions.LowRiskCount < 0 ||
                predictions.HighRiskCount + predictions.MediumRiskCount + predictions.LowRiskCount !=
                predictions.TotalProcessed ||
                predictions.RanAt == default ||
                predictions.Results.Any(result => !IsValidPrediction(result)) ||
                predictions.Results.Count(result => result.RiskLevel == "HighRisk") != predictions.HighRiskCount ||
                predictions.Results.Count(result => result.RiskLevel == "MediumRisk") != predictions.MediumRiskCount ||
                predictions.Results.Count(result => result.RiskLevel == "LowRisk") != predictions.LowRiskCount)
            {
                throw new JsonException("Module 3 AI Service returned an incomplete batch prediction.");
            }

            return predictions;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Module 3 AI Service returned an invalid batch prediction.");
            throw new Module3AiException(502, "AI Service trả về kết quả dự đoán không hợp lệ.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(authorization, out var parsedAuthorization) &&
            string.Equals(parsedAuthorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parsedAuthorization.Parameter))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                parsedAuthorization.Scheme,
                parsedAuthorization.Parameter);
        }

        return request;
    }

    private static bool IsValidPrediction(PredictionResultResponse? prediction) =>
        prediction is not null &&
        prediction.StudentId > 0 &&
        prediction.EnrollmentId > 0 &&
        double.IsFinite(prediction.RenewalProbability) &&
        prediction.RenewalProbability is >= 0 and <= 1 &&
        prediction.RiskLevel is "HighRisk" or "MediumRisk" or "LowRisk" &&
        prediction.DaysUntilPeriodEnd >= 0 &&
        prediction.TopDrivers is { Count: 3 } &&
        prediction.TopDrivers.All(driver =>
            driver is not null &&
            !string.IsNullOrWhiteSpace(driver.Feature) &&
            !string.IsNullOrWhiteSpace(driver.Direction) &&
            double.IsFinite(driver.Impact) &&
            !string.IsNullOrWhiteSpace(driver.Description)) &&
        !string.IsNullOrWhiteSpace(prediction.ModelVersion) &&
        prediction.PredictionDate != default;

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryReadDetail(body);
        logger.LogWarning(
            "Module 3 AI Service returned HTTP {StatusCode}: {Message}",
            (int)response.StatusCode,
            message);
        throw new Module3AiException((int)response.StatusCode, message);
    }

    private static string TryReadDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "AI Service không trả về thông tin lỗi.";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }
        }
        catch (JsonException)
        {
            return "AI Service trả về lỗi không đúng định dạng.";
        }

        return "AI Service không thể xử lý yêu cầu.";
    }

    private sealed record GenerateCallScriptRequest(
        [property: JsonPropertyName("student_id")] int StudentId,
        [property: JsonPropertyName("question")] string Question);
}
