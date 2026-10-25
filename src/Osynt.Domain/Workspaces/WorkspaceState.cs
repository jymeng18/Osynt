using Osynt.Domain.Garden;

namespace Osynt.Domain.Workspaces;

public sealed class WorkspaceState
{
    public bool UseLocalDemo { get; set; } = true;
    public Guid? DefaultCollectionId { get; set; }
    public bool ConfirmDeletion { get; set; } = true;
    public GardenState? Garden
    {
        get; set;
    }
    public List<RequestCollection> Collections { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = new() { ["base_url"] = "", ["season"] = "spring" };

    public WorkspaceState Clone() => new()
    {
        UseLocalDemo = UseLocalDemo,
        DefaultCollectionId = DefaultCollectionId,
        ConfirmDeletion = ConfirmDeletion,
        Garden = Garden?.Clone(),
        Variables = new(Variables),
        Collections = Collections.Select(c => c.Clone()).ToList(),
        History = History.Select(h => h.Clone()).ToList()
    };
}
