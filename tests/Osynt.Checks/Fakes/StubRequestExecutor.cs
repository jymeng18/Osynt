using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;

namespace Osynt.Checks.Fakes;

internal sealed class StubRequestExecutor(
    Func<RequestDocument, IReadOnlyDictionary<string, string>, CancellationToken, Task<ResponseData>> send)
    : IRequestExecutor
{
    public Task<ResponseData> SendAsync(RequestDocument document,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default) =>
        send(document, variables, cancellationToken);
}
