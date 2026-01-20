using Core.Services;

namespace Infrastructure.Services;

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
