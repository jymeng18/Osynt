namespace Osynt.Domain.Workspaces;

public sealed class WorkspaceState
{
    public bool UseLocalDemo { get; set; } = true;
    public List<RequestCollection> Collections { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = new() { ["base_url"] = "", ["season"] = "spring" };
}
