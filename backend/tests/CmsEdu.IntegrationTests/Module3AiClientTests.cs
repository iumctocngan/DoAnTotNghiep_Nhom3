using System.Net;
using System.Text;
using CmsEdu.Api.Services;
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
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
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
            NullLogger<Module3AiClient>.Instance);

        var response = await client.GenerateCallScriptAsync(42, "Tình hình học tập?", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("/api/ai/cskh/call-script", capturedRequest.RequestUri!.AbsolutePath);
        var requestBody = await capturedRequest.Content!.ReadAsStringAsync();
        using var requestJson = System.Text.Json.JsonDocument.Parse(requestBody);
        Assert.Equal(42, requestJson.RootElement.GetProperty("student_id").GetInt32());
        Assert.Equal("Tình hình học tập?", requestJson.RootElement.GetProperty("question").GetString());
        Assert.Equal("Tóm tắt", response.Summary);
        Assert.Equal("Nguy cơ", response.RiskAnalysis);
        Assert.Equal(
            ["Chào hỏi", "Thông báo tình trạng", "Thăm dò khó khăn", "Đề xuất lộ trình", "Cam kết hỗ trợ"],
            response.SuggestedCallScript);
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
            NullLogger<Module3AiClient>.Instance);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
