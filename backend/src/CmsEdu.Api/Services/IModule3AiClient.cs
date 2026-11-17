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
    Task<PredictionResultResponse> PredictStudentAsync(
        int studentId,
        int enrollmentId,
        CancellationToken cancellationToken);
    Task<BatchPredictionResponse> PredictBatchAsync(CancellationToken cancellationToken);
}

public sealed record PredictStudentRequest(
    [property: JsonPropertyName("student_id")] int StudentId,
    [property: JsonPropertyName("enrollment_id")] int EnrollmentId);

public sealed record ShapDriverResponse(
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("impact")] double Impact,
    [property: JsonPropertyName("description")] string Description);

public sealed record PredictionResultResponse(
    [property: JsonPropertyName("student_id")] int StudentId,
    [property: JsonPropertyName("enrollment_id")] int EnrollmentId,
    [property: JsonPropertyName("renewal_probability")] double RenewalProbability,
    [property: JsonPropertyName("risk_level")] string RiskLevel,
    [property: JsonPropertyName("days_until_period_end")] int DaysUntilPeriodEnd,
    [property: JsonPropertyName("top_drivers")] IReadOnlyList<ShapDriverResponse> TopDrivers,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("prediction_date")] DateOnly PredictionDate);

public sealed record BatchPredictionResponse(
    [property: JsonPropertyName("total_processed")] int TotalProcessed,
    [property: JsonPropertyName("high_risk_count")] int HighRiskCount,
    [property: JsonPropertyName("medium_risk_count")] int MediumRiskCount,
    [property: JsonPropertyName("low_risk_count")] int LowRiskCount,
    [property: JsonPropertyName("results")] IReadOnlyList<PredictionResultResponse> Results,
    [property: JsonPropertyName("ran_at")] DateTime RanAt);

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
