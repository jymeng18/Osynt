using Osynt.Domain.Requests;

namespace Osynt.Application.Abstractions;

public interface IRequestExecutor
{
    Task<ResponseData> SendAsync(RequestDocument document, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);
}
