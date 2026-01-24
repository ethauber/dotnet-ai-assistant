namespace Core.Services;

/// <summary>
/// Defines a contract for reporting the overall health status of the application core.
/// Implementations are responsible for aggregating internal signals into a simple
/// status representation that can be consumed across layers (e.g., API, infrastructure).
/// </summary>
public interface IHealthStatusService
{
    /// <summary>
    /// Gets the current health status of the application.
    /// </summary>
    /// <remarks>
    /// Typical values are:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>Healthy</c> - All required dependencies are functioning within expected thresholds.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>Degraded</c> - The application is still operational, but one or more non-critical
    /// components are impaired or operating outside ideal thresholds.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>Unhealthy</c> - The application is not able to reliably serve requests.
    /// </description>
    /// </item>
    /// </list>
    /// Consumers should not rely on these exact string values, but treat them as
    /// human-readable summaries of the current health state.
    /// </remarks>
    /// <returns>
    /// A string representing the current health status of the application.
    /// </returns>
    string GetStatus();

    /// <summary>
    /// Sets whether the application is currently operating in a degraded state.
    /// </summary>
    /// <param name="isDegraded">
    /// <see langword="true"/> to mark the application as degraded (still serving requests
    /// but with reduced quality or partial impairment of non-critical dependencies);
    /// <see langword="false"/> to clear the degraded state.
    /// </param>
    /// <remarks>
    /// The degraded state is intended to signal to upstream layers (such as the API layer
    /// or external monitoring) that the system is experiencing partial impairment but does
    /// not require immediate failover or shutdown. Implementations may incorporate this
    /// flag into the value returned by <see cref="GetStatus"/>.
    /// </remarks>
    void SetDegraded(bool isDegraded);
}
