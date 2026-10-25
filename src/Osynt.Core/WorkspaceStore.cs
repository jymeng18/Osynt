using System.Text.Json;

namespace Osynt.Core;

public sealed class WorkspaceStore(string? directory = null)
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; } = directory ?? Environment.GetEnvironmentVariable("OSYNT_DATA_DIR") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Osynt");
    public string FilePath => Path.Combine(DirectoryPath, "workspace.json");
    public string? LoadNotice { get; private set; }

    public WorkspaceState Load()
    {
        if (!File.Exists(FilePath)) return WorkspaceState.CreateDefault();
        try
        {
            var workspace = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(FilePath), JsonOptions)
                ?? throw new JsonException("Empty workspace");
            if (workspace.Collections is null || workspace.History is null || workspace.Variables is null ||
                workspace.Collections.Any(c => c is null || c.Requests is null || c.Requests.Any(r =>
                    r is null || r.Parameters is null || r.Headers is null || r.Name is null || r.Method is null || r.Url is null || r.Body is null)))
                throw new JsonException("Invalid workspace structure");
            return workspace;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadNotice = "Your saved workspace could not be read. It has been left untouched; a fresh workspace is open.";
            return WorkspaceState.CreateDefault();
        }
    }

    public void Save(WorkspaceState workspace)
    {
        Directory.CreateDirectory(DirectoryPath);
        if (LoadNotice is not null && File.Exists(FilePath))
        {
            File.Copy(FilePath, Path.Combine(DirectoryPath, $"workspace-unreadable-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json"));
            LoadNotice = null;
        }
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(workspace, JsonOptions));
        File.Move(temporary, FilePath, true);
    }
}
