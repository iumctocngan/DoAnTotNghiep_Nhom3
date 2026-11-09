using System.Text.Json;
using System.Text.Json.Serialization;

namespace CmsEdu.Api.Services;

public interface IModule3AiClient
{
    Task<JsonElement> GetStudentContextAsync(int studentId, CancellationToken cancellationToken);
    Task<AiCallScriptResponse> GenerateCallScriptAsync(
        int studentId,
        string question,
        CancellationToken cancellationToken);
}

public sealed record AiCallScriptResponse(
    [property: JsonPropertyName("summary")]
    string Summary,
    [property: JsonPropertyName("risk_analysis")]
    string RiskAnalysis,
    [property: JsonPropertyName("action_plan")]
    string ActionPlan,
    [property: JsonPropertyName("suggested_call_script")]
    IReadOnlyList<string> SuggestedCallScript);

public sealed class Module3AiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
