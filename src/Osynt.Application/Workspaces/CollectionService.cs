using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Application.Workspaces;

public sealed class CollectionService(WorkspaceSession session, ICollectionSerializer serializer)
{
    public IReadOnlyList<RequestCollection> GetCollections() =>
        session.Read(state => state.Collections.Select(c => c.Clone()).ToList());

    public bool ConfirmDeletion => session.Read(state => state.ConfirmDeletion);

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

    public void DeleteRequest(Guid requestId, bool dontAskAgain = false) => session.Commit(state =>
    {
        foreach (var collection in state.Collections)
            collection.Requests.RemoveAll(r => r.Id == requestId);
        if (dontAskAgain)
            state.ConfirmDeletion = false;
    });

    public IReadOnlyList<Guid> DeleteCollection(Guid collectionId, bool dontAskAgain = false)
    {
        IReadOnlyList<Guid> deletedRequests = [];
        session.Commit(state =>
        {
            var collection = state.Collections.FirstOrDefault(c => c.Id == collectionId);
            if (collection is null)
                return;
            deletedRequests = collection.Requests.Select(r => r.Id).ToArray();
            state.Collections.Remove(collection);
            if (state.DefaultCollectionId == collectionId)
                state.DefaultCollectionId = null;
            if (dontAskAgain)
                state.ConfirmDeletion = false;
        });
        return deletedRequests;
    }

    public void RenameRequest(Guid requestId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter a request name.");
        session.Commit(state =>
        {
            var request = state.Collections.SelectMany(c => c.Requests).FirstOrDefault(r => r.Id == requestId)
                ?? throw new InvalidOperationException("This request no longer exists.");
            request.Name = name.Trim();
        });
    }

    public void Rename(Guid collectionId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter a folder name.");
        session.Commit(state =>
        {
            var collection = state.Collections.FirstOrDefault(c => c.Id == collectionId)
                ?? throw new InvalidOperationException("This folder no longer exists.");
            if (state.DefaultCollectionId is null && collection.Name is "My requests" or "Your requests")
                state.DefaultCollectionId = collection.Id;
            collection.Name = name.Trim();
        });
    }

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
        var collection = state.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == requestId));
        if (collection is not null)
            return collection;

        collection = state.Collections.FirstOrDefault(c => c.Id == state.DefaultCollectionId)
            ?? state.Collections.FirstOrDefault(c => c.Name is "My requests" or "Your requests");
        if (collection is null)
        {
            collection = new RequestCollection { Name = "My requests" };
            state.Collections.Add(collection);
        }
        state.DefaultCollectionId = collection.Id;
        return collection;
    }
}
