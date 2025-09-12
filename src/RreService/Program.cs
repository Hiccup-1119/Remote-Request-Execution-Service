using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.aspNetCore.Http.HttpResults;
using Rreservice.Abstractions;
using Rreservice.Executors;
using Rreservice.Observability;
using Rreservice.Orchestration;
using Rreservice.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton(new Metrics());

builder.Services.AddHttpClient<HttpExecutor>();
builder.Services.AddSingleton<IExecutor>(sp => sp.GetRequiredService<HttpExecutor>());
builder.Services.AddSingletonM<IExecutor, PowerShellExecutor>();
builder.Services.AddHttpClient(new HeaderFileter());

var cfg = builder.Configuration;
var maxAttempts = cfg.GetValue<int?>("Resilience:MaxAttempts", 3);
var baseDelayMs = cfg.GetValue<int?>("Resilience:BaseDelayMs", 200);
var maxDelayMs = cfg.GetValue<int?>("Resilience:MaxDelayMs", 2000);

builder.Services.AddSingleton(sp => new RetryPolicy(
    sp.GetRequiredService<IClock>(),
    maxAttempts,
    TimeSpan.FromMilliseconds(baseDelayMs),
    TimeSpan.FromMilliseconds(maxDelayMs)));

builder.Services.AddSingleton(sp => new Orchestrator(
    sp.GetRequiredService<IExecutor>(),
    sp.GetRequiredService<RetryPolicy>(),
    sp.GetRequiredService<IClock>(),
    sp.GetRequiredService<Metrics>(),
    sp.GetRequiredService<Ilogger<Orchestrator>>(),
    instanceId: Environment.GetEnvironmentVariable("INSTANCE_ID" ?? Guid.NewGuid().ToString("n"))
    ));

var app = builder.Build();

app.MapGet("/ping", () => Results.Ok("pong"));
app.Mapget("/metrics", (Metrics m) =>
{
    var (avg, p95) = m.Snapshot();
    return Results.Json(new{
        total = m.RequestsReceived,
        success = m.RequestsSucceeded,
        failed = m.RequestFailed,
        retried = m.RequestRetried,
        avgLatencyMs = avg,
        p95LatencyMs = p95
    })
});

app.MapMethods("/api/{**path}", new[] { "GET", "POST", "PUT", "DELETE", "PATCH" }, async (HttpContext ctx, string path, Orchestrator orch) =>
{
    var executor = http.Request.Headers["X-Executor"].ToString();
    if(string.IsNullOrWhiteSpace(executor))
    {
        executor = "http";
    }
    string? bodyText = null;
    if(http.Request.ContentLength > 0)
    {
        using var sr = new StreamReader(http.Request.Body);
        bodyText = await reader.ReadToEndAsync();
    }

    var req = new NormalizedRequest
    (
        Executor: executor,
        requestId: http.Request.Headers["X-Request-ID"],
        CorrelationId: http.Request.Headers["X-Correlation-ID"],
        TimeoutMs: null,
        Http: executor == "http" ? new HttpSpec(
            BaseUrl: http.Request.Headers["X-Base-Url"],
            Path: http.Request.RouteValues.TryGetValue("path", out var p) ? p?.ToString() : string.Empty,
            Method: http.Request.Method,
            Query: http.Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
            Headers: http.Request.Headers.Where(h => h.Key.StartsWith("X-Forward-", StringComparison.OrdinalIgnoreCase) || h.Key is "Authorization" or "Accept" or "Content-Type")
            .ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body: string.IsNullOrWhiteSpace(bodyText) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(bodyText)
        ) : null,
        PowerShell: executor == "powershell" ? new PowerShellSpec(
        Operation: http.Request.Headers["X-RRE-PS-Operation"],
        Parameters: http.Request.Headers.Where(h => h.Key.StartsWith("X-RRE-PS-Param-"))
        .ToDictionary(h => h.Key.Replace("X-RRE-PS-Param-", ""), h => h.Value.ToString()),
        Paging: null,
        TenantKey: http.Request.Headers["X-RRE-Tenant"]
        ) : null
    );

    var result = await orch.HandleAsync(req, http.RequestAborted);


    http.Response.Headers["X-RRE-Request-Id"] = result.RequestId;
    http.Response.Headers["X-RRE-Correlation-Id"] = result.CorrelationId ?? string.Empty;
    http.Response.Headers["X-RRE-Executor"] = result.ExecutorType;
    http.Response.Headers["X-RRE-Attempts"] = result.AttemptCount.ToString();

    return Results.Json(result, new JsonSerializerOptions
    {
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });
});

app.MapGet("/", () => "Hello World!");

app.Run();
