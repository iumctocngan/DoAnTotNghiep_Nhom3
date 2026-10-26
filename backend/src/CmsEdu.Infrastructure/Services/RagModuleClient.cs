using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CmsEdu.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CmsEdu.Infrastructure.Services;

public class RagModuleClient(
    HttpClient client,
    IHttpContextAccessor contextAccessor,
    IConfiguration config) : IRagModuleClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private string BaseUrl => (config["RagService:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}{path}") { Content = content };
        var authHeader = contextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authHeader["Bearer ".Length..].Trim());
        return request;
    }

    private async Task<bool> SendDeleteAsync(string path, CancellationToken ct)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Delete, path);
            return (await client.SendAsync(req, ct)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> IngestDocumentAsync(Guid notebookId, Guid documentId, string fileName, Stream fileStream, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(documentId.ToString()), "document_id");
        using var req = CreateRequest(HttpMethod.Post, $"/api/v1/notebooks/{notebookId}/documents/ingest", content);
        return (await client.SendAsync(req, ct)).IsSuccessStatusCode;
    }

    public Task<bool> DeleteDocumentAsync(Guid notebookId, Guid documentId, CancellationToken ct) =>
        SendDeleteAsync($"/api/v1/notebooks/{notebookId}/documents/{documentId}", ct);

    public Task<bool> DeleteNotebookAsync(Guid notebookId, CancellationToken ct) =>
        SendDeleteAsync($"/api/v1/notebooks/{notebookId}", ct);

    public async Task<ChatResponseDto> ChatAsync(Guid notebookId, ChatRequestDto requestDto, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(requestDto, JsonOptions), Encoding.UTF8, "application/json");
        using var req = CreateRequest(HttpMethod.Post, $"/api/v1/notebooks/{notebookId}/chat", content);
        var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ChatResponseDto>(body, JsonOptions) ?? new("Không nhận được câu trả lời từ hệ thống RAG.", []);
    }

    public async Task<(byte[] Bytes, string FileName)> GenerateCloRubricAsync(CloRubricRequestDto requestDto, CancellationToken ct)
    {
        var payload = requestDto with { OutcomeCount = requestDto.OutcomeCount ?? 3 };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var req = CreateRequest(HttpMethod.Post, "/api/v1/clo-rubric/generate", content);
        var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var bytes = await res.Content.ReadAsByteArrayAsync(ct);
        var fn = res.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return (bytes, !string.IsNullOrWhiteSpace(fn) ? fn : $"clo-rubric-{requestDto.DocumentId}.xlsx");
    }

    public async Task<Stream> GetPageImageStreamAsync(Guid notebookId, Guid documentId, int page, CancellationToken ct)
    {
        using var req = CreateRequest(HttpMethod.Get, $"/api/v1/notebooks/{notebookId}/documents/{documentId}/pages/{page}/image");
        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStreamAsync(ct);
    }

    public async Task<List<DocumentOutlineItemDto>> GetDocumentOutlineAsync(Guid notebookId, Guid documentId, CancellationToken ct)
    {
        using var req = CreateRequest(HttpMethod.Get, $"/api/v1/notebooks/{notebookId}/documents/{documentId}/outline");
        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return [];
        var body = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<DocumentOutlineItemDto>>(body, JsonOptions) ?? [];
    }
}
