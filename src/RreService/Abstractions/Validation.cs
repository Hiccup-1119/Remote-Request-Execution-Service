using System.Text.Json;

namespace Rreservice.Abstractions;

public static class Validation
{
    public static (bool ok, string? error) Validate(NormalizedRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Executor))
            return (false, "executor required");
        if (req.Executor is not ("http" or "powershell"))
            return (false, $"unsupported executor '{req.Executor}'");
        if (req.Executor == "http" && req.Http is null)
            return (false, "http spec required for http executor");
        if (req.Executor == "powershell" && req.PowerShell is null)
            return (false, "powershell spec required for powershell executor");
        if (req.Http is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Http.BaseUrl)) return (false, "http.baseUrl required");
            if (string.IsNullOrWhiteSpace(req.Http.Method)) return (false, "http.method required");
        }
        if (req.PowerShell is not null)
        {
            if (string.IsNullOrWhiteSpace(req.PowerShell.Operation)) return (false, "powershell.operation required");
        }
        return (true, null);
    }
}