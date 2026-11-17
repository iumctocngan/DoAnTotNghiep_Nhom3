using System.Net;
using System.Text;
using CmsEdu.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CmsEdu.IntegrationTests;

public class Module3AiClientTests
{
    [Fact]
    public async Task GetStudentContextAsync_ReturnsOriginalJsonShape()
    {
        var client = CreateClient(
            HttpStatusCode.OK,
            """{"student_id":42,"student_name":"An"}""");

        var context = await client.GetStudentContextAsync(42, CancellationToken.None);

        Assert.Equal(42, context.GetProperty("student_id").GetInt32());
        Assert.Equal("An", context.GetProperty("student_name").GetString());
    }

    [Fact]
    public async Task GenerateCallScriptAsync_UsesExpectedRequestAndReadsResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedRequestBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedRequestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"summary":"Tóm tắt","risk_analysis":"Nguy cơ","action_plan":"Liên hệ","suggested_call_script":["Chào hỏi","Thông báo tình trạng","Thăm dò khó khăn","Đề xuất lộ trình","Cam kết hỗ trợ"]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new Module3AiClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001/") },
            new HttpContextAccessor(),
            NullLogger<Module3AiClient>.Instance);

        var response = await client.GenerateCallScriptAsync(42, "Tình hình học tập?", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("/api/ai/cskh/call-script", capturedRequest.RequestUri!.AbsolutePath);
        using var requestJson = System.Text.Json.JsonDocument.Parse(capturedRequestBody!);
        Assert.Equal(42, requestJson.RootElement.GetProperty("student_id").GetInt32());
        Assert.Equal("Tình hình học tập?", requestJson.RootElement.GetProperty("question").GetString());
        Assert.Equal("Tóm tắt", response.Summary);
        Assert.Equal("Nguy cơ", response.RiskAnalysis);
        Assert.Equal(
            ["Chào hỏi", "Thông báo tình trạng", "Thăm dò khó khăn", "Đề xuất lộ trình", "Cam kết hỗ trợ"],
            response.SuggestedCallScript);
    }

    [Fact]
    public async Task PredictStudentAsync_ForwardsBearerTokenAndReadsPrediction()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"student_id":42,"enrollment_id":9,"renewal_probability":0.72,"risk_level":"HighRisk","days_until_period_end":5,"top_drivers":[{"feature":"Attendance","direction":"negative","impact":-0.2,"description":"Đi học không đều"},{"feature":"Debt","direction":"negative","impact":0.1,"description":"Còn công nợ"},{"feature":"Score","direction":"positive","impact":0.05,"description":"Kết quả học tập"}],"model_version":"v1","prediction_date":"2026-03-01"}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer forwarded-token";
        var client = new Module3AiClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001/") },
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<Module3AiClient>.Instance);

        var response = await client.PredictStudentAsync(42, 9, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("/api/ml/predict-student", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("forwarded-token", capturedRequest.Headers.Authorization.Parameter);
        Assert.Equal(0.72, response.RenewalProbability);
        Assert.Equal(9, response.EnrollmentId);
    }

    [Fact]
    public async Task PredictStudentAsync_RejectsIncompletePrediction()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"student_id":42,"enrollment_id":9}""");

        var exception = await Assert.ThrowsAsync<Module3AiException>(
            () => client.PredictStudentAsync(42, 9, CancellationToken.None));

        Assert.Equal(502, exception.StatusCode);
    }

    [Fact]
    public async Task PredictBatchAsync_UsesExpectedEndpointAndReadsResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"total_processed":0,"high_risk_count":0,"medium_risk_count":0,"low_risk_count":0,"results":[],"ran_at":"2026-03-01T12:00:00Z"}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var client = new Module3AiClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001/") },
            new HttpContextAccessor(),
            NullLogger<Module3AiClient>.Instance);

        var response = await client.PredictBatchAsync(CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("/api/ml/predict-batch", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal(0, response.TotalProcessed);
    }

    [Fact]
    public async Task GenerateCallScriptAsync_PropagatesUpstreamProblemDetail()
    {
        var client = CreateClient(
            HttpStatusCode.ServiceUnavailable,
            """{"detail":"Gemini chưa được cấu hình."}""");

        var exception = await Assert.ThrowsAsync<Module3AiException>(
            () => client.GenerateCallScriptAsync(42, "Tư vấn", CancellationToken.None));

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("Gemini chưa được cấu hình.", exception.Message);
    }

    [Fact]
    public async Task GenerateCallScriptAsync_RejectsInvalidUpstreamJson()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"summary":"Thiếu các trường bắt buộc"}""");

        var exception = await Assert.ThrowsAsync<Module3AiException>(
            () => client.GenerateCallScriptAsync(42, "Tư vấn", CancellationToken.None));

        Assert.Equal(502, exception.StatusCode);
    }

    private static Module3AiClient CreateClient(HttpStatusCode statusCode, string body) =>
        new(
            new HttpClient(new StubHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                })))
            {
                BaseAddress = new Uri("http://localhost:8001/")
            },
            new HttpContextAccessor(),
            NullLogger<Module3AiClient>.Instance);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
