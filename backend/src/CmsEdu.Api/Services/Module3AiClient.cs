using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CmsEdu.Api.Services;

public sealed class Module3AiClient(HttpClient httpClient, ILogger<Module3AiClient> logger) : IModule3AiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<JsonElement> GetStudentContextAsync(int studentId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
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
        using var response = await httpClient.PostAsJsonAsync(
            "api/ai/cskh/call-script",
            new GenerateCallScriptRequest(studentId, question),
            JsonOptions,
            cancellationToken);
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
