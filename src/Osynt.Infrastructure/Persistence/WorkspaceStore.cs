using Osynt.Application.Abstractions;
using Osynt.Domain.Workspaces;

using System.Text.Json;

namespace Osynt.Infrastructure.Persistence;

public sealed class WorkspaceStore(string? directory = null) : IWorkspaceStore
{
    private readonly object _gate = new();
    private bool _preserveUnreadable;
    public string DirectoryPath
    {
        get;
    } = directory ?? Environment.GetEnvironmentVariable("OSYNT_DATA_DIR") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Osynt");
    public string FilePath => Path.Combine(DirectoryPath, "workspace.json");
    public string BackupFilePath => FilePath + ".bak";
    public string? LoadNotice
    {
        get; private set;
    }

    public WorkspaceState? Load()
    {
        lock (_gate)
        {
            LoadNotice = null;
            _preserveUnreadable = false;
            if (TryRead(FilePath) is { } workspace)
                return workspace;
            _preserveUnreadable = File.Exists(FilePath);
            if (TryRead(BackupFilePath) is { } backup)
            {
                LoadNotice = "Your workspace was recovered from the previous saved backup. Any unreadable file will be preserved on the next save.";
                return backup;
            }
            if (_preserveUnreadable || File.Exists(BackupFilePath))
                LoadNotice = "Your saved workspace could not be read. It has been left untouched; a fresh workspace is open.";
            return null;
        }
    }

    private static WorkspaceState? TryRead(string path)
    {
        try
        {
            var workspace = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(path), WorkspaceJson.Options);
            return workspace is not null && WorkspaceSchema.IsValid(workspace) ? workspace : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(WorkspaceState workspace)
    {
        lock (_gate)
        {
            if (!WorkspaceSchema.IsValid(workspace))
                throw new IOException("The workspace contains invalid data and could not be saved.");
            Directory.CreateDirectory(DirectoryPath);
            var temporary = Path.Combine(DirectoryPath, $"workspace-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, workspace, WorkspaceJson.Options);
                    stream.Flush(flushToDisk: true);
                }
                if (_preserveUnreadable && File.Exists(FilePath))
                {
                    File.Copy(FilePath, Path.Combine(DirectoryPath, $"workspace-unreadable-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json"));
                    File.Move(temporary, FilePath, overwrite: true);
                }
                else if (File.Exists(FilePath))
                    File.Replace(temporary, FilePath, BackupFilePath);
                else
                    File.Move(temporary, FilePath);
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            _preserveUnreadable = false;
            LoadNotice = null;
        }
    }
}
