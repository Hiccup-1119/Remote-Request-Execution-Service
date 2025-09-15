using System.Text.Json;
using RreService.Abstractions;

namespace RreService.Executors;

public sealed class PowerShellExecutor : IExecutor
{
    public string Type => "powershell";

    private static readonly Dictionary<string, string> Allowlist = new()
    {
        // logical op -> command mapping (prevent arbitrary)
        ["ListMailboxes"] = "Get-EXOMailbox",
        ["ListGroups"] = "Get-EXOGroup"
    };

    public Task<AttemptResult> ExecuteAsync(NormalizedRequest req, CancellationToken ct)
    {
        var spec = req.PowerShell!;
        if (!Allowlist.TryGetValue(spec.Operation, out var command))
        {
            return Task.FromResult(new AttemptResult
            {
                Outcome = AttemptOutcome.PermanentFailure,
                Payload = JsonDocument.Parse("{}"),
                Error = $"Operation '{spec.Operation}' not allowed"
            });
        }
        // In a real impl: create remote session (per-tenant key), run command with allowed parameters, dispose session.
        // For safety in this sample, we simulate results.
        var objects = new[]
        {
            new { DisplayName = "Sample Mailbox", PrimarySmtpAddress = "sample@example.com" },
            new { DisplayName = "Another", PrimarySmtpAddress = "another@example.com" }
        };
        var payload = JsonSerializer.SerializeToDocument(new
        {
            command,
            parameters = spec.Parameters,
            stdout = "(simulated)",
            stderr = string.Empty,
            objects
        });
        return Task.FromResult(new AttemptResult { Outcome = AttemptOutcome.Success, Payload = payload });
    }
}
