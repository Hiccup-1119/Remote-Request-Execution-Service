namespace Rreservice.Abstractions;

public interface IClock{
    DateTime UtcNow{get;}
    Task DelayAsync(TimeSpan d, CancellationToken ct);
}

public sealed class SystemClock : IClock{
    public DateTime UtcNow => DateTime.UtcNow;
    public Task DelayAsync(TimeSpan d, CancellationToken ct) => Task.Delay(d, ct);
}