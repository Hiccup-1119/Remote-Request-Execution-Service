using RreService.Abstractions;
using RreService.Resilience;
using Xunit;

public class RetryPolicyTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; private set; } = DateTime.UtcNow;
        public Task DelayAsync(TimeSpan d, CancellationToken ct) { UtcNow += d; return Task.CompletedTask; }
    }

    [Fact]
    public async Task TimesOut()
    {
        var clock = new FakeClock();
        var policy = new RetryPolicy(clock, 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2));
        var perAttempt = TimeSpan.FromMilliseconds(1);
        var (final, attempts) = await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return new AttemptResult { Outcome = AttemptOutcome.Success, Payload = System.Text.Json.JsonDocument.Parse("{}") };
        }, perAttempt, CancellationToken.None);
        Assert.Equal(AttemptOutcome.Timeout, final.Outcome);
        Assert.True(attempts.Count >= 1);
    }
}