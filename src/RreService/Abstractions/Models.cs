using System.Text.Json.Serialization;

namespace Rreservice.Abstractions;

public record normalizedRequest(
    string Executor,
    string? RequestId,
    string? CorrelationId,
    int? TimeoutMs,
    httpSpec? Http,
    PowerShellSpec? PowerShell,
);

public record httpSpec(
    string BaseUrl,
    string? path,
    string Method,
    Dictionary<string, string>? Query,
    Dictionary<string, string>? Headers,
    JsonElement? Body
);

public record PowerShellSpec(
    string Operation,
    Dictionary<string, string>? Parameters,
    Paging? Paging,
    string? TenantKey
);

public record Paging(
    int? PageSize, 
    string? ContinuationToken
);

public enum AttemptOutcome{
    Success,
    TransientFailure,
    PermanentFailure,
    Timeout
};

public record AttemptSummary(
    int Attempt,
    AttemptOutcome Outcome,
    int DurationMs,
    string? Error
);

public record ResponseEnvelope(
    string RequestId,
    string? CorrelationId,
    string ExecutorType,
    DateTime StartedUtc,
    DateTime EndedUtc,
    string Status,
    int AttemptCount,
    IReadOnlyList<AttemptSummary> Attempts,
    JsonElement? Result,
);