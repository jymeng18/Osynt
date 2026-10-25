using System.Text.Json;
using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Infrastructure.Persistence;

public sealed class JsonCollectionSerializer : ICollectionSerializer
{
    public string Serialize(IReadOnlyList<RequestCollection> collections) => JsonSerializer.Serialize(collections, WorkspaceJson.Options);
    public IReadOnlyList<RequestCollection> Deserialize(string text)
    {
        var collections = JsonSerializer.Deserialize<List<RequestCollection>>(text, WorkspaceJson.Options)
            ?? throw new JsonException("No collections were found.");
        if (!collections.All(WorkspaceSchema.IsValid))
            throw new JsonException("This file contains an invalid collection or HTTP method.");
        return collections;
    }
}
