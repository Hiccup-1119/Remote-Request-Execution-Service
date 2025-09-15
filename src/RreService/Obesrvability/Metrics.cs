using System.Collections.Concurrent;

namespace RreService.Observability;

public sealed class Metrics{
    public long RequestsReceived;
    public long RequestsSucceeded;
    public long RequestsFailed;
    public long RequestsRetried;

    private readonly int _window = 1024;
    private readonly ConcurrentQueue<long> _latencyMs = new();

    public void ObserveLatency(long ms){
        _latencyMs.Enqueue(ms);
        while(_latencyMs.Count > _window && _latencyMs.TryDequeue(out _)){}
    }

    public (double avg, double p95) Snapshot()
    {
        var arr = _latencyMs.ToArray();
        if(arr.Length == 0) return (0,0);
        var avg = arr.Average();
        Array.Sort(arr);
        var idx = (int)Math.Ceiling(arr.Length * 0.95) - 1;
        idx = Math.Clamp(idx, 0, arr.Length - 1);
        var p95 = (double)arr[idx];
        return (avg, p95);
    }
}