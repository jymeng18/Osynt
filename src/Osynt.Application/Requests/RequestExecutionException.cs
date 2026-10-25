namespace Osynt.Application.Requests;

/// <summary>An expected execution failure, expressed without transport-specific exception types.</summary>
public sealed class RequestExecutionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
