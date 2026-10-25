using Osynt.Domain.Requests;

namespace Osynt.Application.Abstractions;

public interface IRequestExecutor
{
    /// <summary>Returns an HTTP response, or throws RequestExecutionException for an expected failure.
    /// Caller cancellation propagates as OperationCanceledException.</summary>
    Task<ResponseData> SendAsync(RequestDocument document, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);
}
