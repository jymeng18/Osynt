using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Application.Workspaces;

public sealed class CollectionService(WorkspaceSession session, ICollectionSerializer serializer)
{
    public IReadOnlyList<RequestCollection> GetCollections() =>
        session.Read(state => state.Collections.Select(c => c.Clone()).ToList());

    public void SaveRequest(RequestDocument request)
    {
        session.Commit(state =>
        {
            var collection = FindCollection(state, request.Id);
            var index = collection.Requests.FindIndex(r => r.Id == request.Id);
            if (index < 0)
                collection.Requests.Add(request.Clone());
            else
                collection.Requests[index] = request.Clone();
        });
    }

    public string Export() => serializer.Serialize(GetCollections());

    public int Import(string text)
    {
        var collections = serializer.Deserialize(text);
        session.Commit(state =>
        {
            foreach (var collection in collections)
                state.Collections.Add(new RequestCollection
                {
                    Name = collection.Name,
                    Requests = collection.Requests.Select(r => r.Clone(newId: true)).ToList()
                });
        });
        return collections.Count;
    }

    private static RequestCollection FindCollection(WorkspaceState state, Guid requestId)
    {
        var collection = state.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == requestId))
            ?? state.Collections.FirstOrDefault(c => c.Name == "My requests");
        if (collection is not null)
            return collection;
        collection = new RequestCollection { Name = "My requests" };
        state.Collections.Add(collection);
        return collection;
    }
}
