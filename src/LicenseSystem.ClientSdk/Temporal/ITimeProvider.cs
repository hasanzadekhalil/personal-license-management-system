namespace LicenseSystem.ClientSdk.Temporal;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    long MonotonicTicks { get; }
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public long MonotonicTicks => Environment.TickCount64;
}

public class MockTimeProvider : ITimeProvider
{
    private DateTime _utcNow;
    private long _monotonicTicks;

    public MockTimeProvider(DateTime? initialUtc = null)
    {
        _utcNow = initialUtc ?? DateTime.UtcNow;
        _monotonicTicks = 100000;
    }

    public DateTime UtcNow => _utcNow;
    public long MonotonicTicks => _monotonicTicks;

    public void AdvanceTime(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
        _monotonicTicks += (long)duration.TotalMilliseconds;
    }

    public void RollbackTime(TimeSpan duration)
    {
        // Rolls back system clock, simulating user tampering
        _utcNow = _utcNow.Subtract(duration);
    }
}
