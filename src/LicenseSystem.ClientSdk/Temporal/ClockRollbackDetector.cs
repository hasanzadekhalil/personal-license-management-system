namespace LicenseSystem.ClientSdk.Temporal;

public class ClockRollbackDetector
{
    private DateTime _lastKnownUtcTime;
    private long _lastKnownMonotonicTicks;
    private readonly TimeSpan _rollbackTolerance;

    public ClockRollbackDetector(DateTime initialTime, TimeSpan? rollbackTolerance = null)
    {
        _lastKnownUtcTime = initialTime;
        _lastKnownMonotonicTicks = 0;
        _rollbackTolerance = rollbackTolerance ?? TimeSpan.FromMinutes(30);
    }

    public bool CheckAndRecordTime(DateTime currentUtc, long currentMonotonicTicks)
    {
        // If current time is earlier than last known time by more than tolerance threshold
        if (currentUtc < (_lastKnownUtcTime - _rollbackTolerance))
        {
            return false; // Clock rollback detected!
        }

        // Monotonic check: If system uptime increased significantly while time went backwards
        if (_lastKnownMonotonicTicks > 0 && currentMonotonicTicks > _lastKnownMonotonicTicks)
        {
            var monotonicElapsed = TimeSpan.FromMilliseconds(currentMonotonicTicks - _lastKnownMonotonicTicks);
            if (currentUtc < _lastKnownUtcTime && monotonicElapsed > TimeSpan.FromMinutes(5))
            {
                return false; // Monotonic clock says time passed, but wall-clock retreated!
            }
        }

        // Update watermarks
        if (currentUtc > _lastKnownUtcTime)
        {
            _lastKnownUtcTime = currentUtc;
        }

        _lastKnownMonotonicTicks = currentMonotonicTicks;
        return true;
    }

    public DateTime LastKnownUtcTime => _lastKnownUtcTime;
}
