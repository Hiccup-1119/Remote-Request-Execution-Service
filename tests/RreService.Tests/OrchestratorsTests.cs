using System.Text.Json;
using RreService.Abstractions;
using RreService.Orchestration;
using RreService.Resilience;
using RreService.Observability;
using Xunit;

public class OrchestratorTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; private set; } = DateTime.UtcNow;
        public Task DelayAsync(TimeSpan d, CancellationToken ct) { UtcNow += d; return Task.CompletedTask; }
    }

    private sealed class FlakyExec : IExecutor
    {
        int _n = 0;
        public string Type => "http";
        public Task<AttemptResult> ExecuteAsync(NormalizedRequest req, CancellationToken ct)
        {
            _n++;
            if (_n < 2) return Task.FromResult(new AttemptResult { Outcome = AttemptOutcome.TransientFailure, Payload = JsonDocument.Parse("{}"), Error = "boom" });
            return Task.FromResult(new AttemptResult { Outcome = AttemptOutcome.Success, Payload = JsonDocument.Parse("{\"ok\":true}") });
        }
    }

    [Fact]
    public async Task RetriesThenSucceeds()
    {
        var clock = new FakeClock();
        var retry = new RetryPolicy(clock, 3, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));
        var metrics = new Metrics();
        var orch = new Orchestrator(new[] { (IExecutor)new FlakyExec() }, retry, clock, metrics, new LoggerFactory().CreateLogger<Orchestrator>(), "test");
        var req = new NormalizedRequest("http", null, null, 1000, new HttpSpec("http://localhost", "/", "GET", null, null, null), null);
        var env = await orch.HandleAsync(req, CancellationToken.None);
        Assert.Equal("Success", env.Status);
        Assert.Equal(2, env.AttemptCount);
    }
}
