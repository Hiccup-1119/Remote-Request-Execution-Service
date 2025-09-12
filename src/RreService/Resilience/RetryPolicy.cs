using RreService.Abstractions;

namespace RreService.Resilience;

public sealed class RetryPolicy(IClock clock, int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
{
    public async Task<(AttemptResult final, List<AttemptSummary> attempts)> ExecuteAsync(Func<CancellationToken, Task<AttemptResult>> attempt,
        TimeSpan perAttemptTimeout, CancellationToken outerCt)
    {
        var attempts = new List<AttemptSummary>();
        for (var i = 1; i <= maxAttempts; i++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            cts.CancelAfter(perAttemptTimeout);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            AttemptResult result;
            try { result = await attempt(cts.Token); }
            finally { sw.Stop(); }

            attempts.Add(new AttemptSummary(i, result.Outcome, (int)sw.ElapsedMilliseconds, result.Error));

            if (result.Outcome == AttemptOutcome.Success)
                return (result, attempts);
            if (result.Outcome == AttemptOutcome.PermanentFailure)
                return (result, attempts);
            if (i == maxAttempts)
                return (result, attempts);

            // exponential backoff with full jitter
            var delay = TimeSpan.FromMilliseconds(Math.Min(maxDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * Math.Pow(2, i - 1)));
            var jitter = Random.Shared.NextDouble();
            var wait = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitter);
            await clock.DelayAsync(wait, outerCt);
        }
        throw new InvalidOperationException("Unreachable");
    }
}
