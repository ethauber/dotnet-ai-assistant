using Core.Services;

namespace Infrastructure.Services;

/// <summary>
/// In-memory implementation of <see cref="IHealthStatusService"/>.
/// The degraded flag is a volatile bool — safe for single-writer, multi-reader scenarios.
/// </summary>
public class HealthStatusService : IHealthStatusService
{
    private volatile bool _isDegraded;

    public void SetDegraded(bool isDegraded)
    {
        _isDegraded = isDegraded;
    }

    public string GetStatus()
    {
        return _isDegraded ? "degraded" : "ok";
    }
}
