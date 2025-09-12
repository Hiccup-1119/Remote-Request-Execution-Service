namespace Rreservice.Abstractions;

public interface IExecutor{
    string Type{get;}
    Task<AttemptResult> ExecuteAsync(normalizedRequest req, CancellationToken ct);
}

public sealed class AttemptResult{
    public AttemptOutcome Outcome{get; init;}
    public JsonDocument Payload{get; init;} = JsonDocument.Parse("{}");
    public string? Error{get; init;}
}