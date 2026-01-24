namespace Api.Models;

/// <summary>
/// Represents the current status of the API as returned by the status endpoint.
/// </summary>
/// <param name="Status">
/// A normalized status value for the API or service. Typical values include:
/// <list type="bullet">
/// <item><description><c>ok</c> - the service is healthy and fully operational.</description></item>
/// <item><description><c>degraded</c> - the service is available but experiencing reduced performance or partial outages.</description></item>
/// </list>
/// Additional status values may be introduced over time.
/// </param>
public record StatusResponse(string Status);
