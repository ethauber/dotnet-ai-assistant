namespace Core.Services;

public interface IHealthStatusService
{
    string GetStatus();
    void SetDegraded(bool isDegraded);
}
