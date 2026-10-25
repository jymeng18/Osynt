using Osynt.Application.Abstractions;
using Osynt.Domain.Workspaces;

using System.Text.Json;

namespace Osynt.Infrastructure.Persistence;

public sealed class WorkspaceStore(string? directory = null) : IWorkspaceStore
{
    public string DirectoryPath
    {
        get;
    } = directory ?? Environment.GetEnvironmentVariable("OSYNT_DATA_DIR") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Osynt");
    public string FilePath => Path.Combine(DirectoryPath, "workspace.json");
    public string? LoadNotice
    {
        get; private set;
    }

    public WorkspaceState? Load()
    {
        if (!File.Exists(FilePath))
            return null;
        try
        {
            var workspace = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(FilePath), WorkspaceJson.Options)
                ?? throw new JsonException("Empty workspace");
            if (!WorkspaceSchema.IsValid(workspace))
                throw new JsonException("Invalid workspace structure");
            return workspace;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadNotice = "Your saved workspace could not be read. It has been left untouched; a fresh workspace is open.";
            return null;
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
        File.WriteAllText(temporary, JsonSerializer.Serialize(workspace, WorkspaceJson.Options));
        File.Move(temporary, FilePath, true);
    }
}
