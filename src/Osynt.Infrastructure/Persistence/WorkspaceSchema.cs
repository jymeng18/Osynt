using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Infrastructure.Persistence;

internal static class WorkspaceSchema
{
    public static bool IsValid(RequestDocument? request) =>
        request is { Name: not null, Url: not null, Body: not null, Parameters: not null, Headers: not null } &&
        request.Id != Guid.Empty &&
        RequestMethods.All.Contains(request.Method) &&
        request.TimeoutSeconds is >= 1 and <= 3600 &&
        request.Parameters.Concat(request.Headers).All(p => p is { Key: not null, Value: not null });

    public static bool IsValid(RequestCollection? collection) =>
        collection is { Requests: not null } && collection.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(collection.Name) && collection.Requests.All(IsValid);

    public static bool IsValid(WorkspaceState state) =>
        state is { Collections: not null, History: not null, Variables: not null } &&
        state.Collections.All(IsValid) && state.History.All(h => h is not null && IsValid(h.Request)) &&
        state.Collections.Select(c => c.Id).Distinct().Count() == state.Collections.Count &&
        UniqueRequestIds(state.Collections) &&
        (state.Garden is null || state.Garden.SuccessfulRequests >= 0 && state.Garden.Atmosphere is "day" or "dusk") &&
        state.Variables.All(v => !string.IsNullOrWhiteSpace(v.Key) && v.Value is not null);

    private static bool UniqueRequestIds(IEnumerable<RequestCollection> collections)
    {
        var ids = new HashSet<Guid>();
        return collections.SelectMany(c => c.Requests).All(r => ids.Add(r.Id));
    }
}
