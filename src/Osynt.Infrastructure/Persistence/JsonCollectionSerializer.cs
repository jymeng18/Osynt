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
        if (collections.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name) || c.Requests is null || c.Requests.Any(r =>
            r is null || r.Name is null || r.Url is null || r.Body is null || r.Parameters is null || r.Headers is null ||
            !RequestMethods.All.Contains(r.Method) || r.Parameters.Concat(r.Headers).Any(p => p is null || p.Key is null || p.Value is null))))
            throw new JsonException("This file contains an invalid collection or HTTP method.");
        return collections;
    }
}
