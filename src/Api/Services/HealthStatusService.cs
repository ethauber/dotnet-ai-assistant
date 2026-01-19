namespace Api.Services;

public interface IHealthStatusService
{
    string GetStatus();
    void SetDegraded(bool isDegraded);
}

public class HealthStatusService : IHealthStatusService
{
    private bool _isDegraded;

    public void SetDegraded(bool isDegraded)
    {
        _isDegraded = isDegraded;
    }

    public string GetStatus()
    {
        return _isDegraded ? "degraded" : "ok";
    }
}
