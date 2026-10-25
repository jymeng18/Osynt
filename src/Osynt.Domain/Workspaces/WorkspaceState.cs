namespace Osynt.Domain.Workspaces;

public sealed class WorkspaceState
{
    public bool UseLocalDemo { get; set; } = true;
    public List<RequestCollection> Collections { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = new() { ["base_url"] = "", ["season"] = "spring" };

    public WorkspaceState Clone() => new()
    {
        UseLocalDemo = UseLocalDemo,
        Variables = new(Variables),
        Collections = Collections.Select(c => new RequestCollection
        {
            Name = c.Name,
            Requests = c.Requests.Select(r => r.Clone()).ToList()
        }).ToList(),
        History = History.Select(h => new HistoryEntry
        {
            SentAt = h.SentAt,
            Request = h.Request.Clone(),
            StatusCode = h.StatusCode,
            ElapsedMilliseconds = h.ElapsedMilliseconds
        }).ToList()
    };
}
