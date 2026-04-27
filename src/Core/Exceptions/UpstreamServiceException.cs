namespace Core.Exceptions;

public sealed class UpstreamServiceException : Exception
{
    public UpstreamServiceException(
        string message,
        int? statusCode = null,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
