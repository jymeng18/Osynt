using Osynt.Domain.Workspaces;

namespace Osynt.Application.Abstractions;

public interface ICollectionSerializer
{
    string Serialize(IReadOnlyList<RequestCollection> collections);
    IReadOnlyList<RequestCollection> Deserialize(string text);
}
