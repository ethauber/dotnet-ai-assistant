namespace Core.Exceptions;

public sealed class ChatServiceUnavailableException : Exception
{
    public ChatServiceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
