using Osynt.Domain.Requests;

namespace Osynt.Application.Abstractions;

public interface ICurlRequestSerializer
{
    RequestDocument Deserialize(string command);
    Task<string> SerializeAsync(RequestDocument request, IReadOnlyDictionary<string, string> variables);
}
