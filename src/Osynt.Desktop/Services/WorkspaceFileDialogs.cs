using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Osynt.Desktop.ViewModels;
using Osynt.Desktop.Presentation;

namespace Osynt.Desktop.Services;

/// <summary>Desktop clipboard and file pickers. File-system concerns stay outside presentation models.</summary>
public sealed class WorkspaceFileDialogs(TopLevel owner, WorkspaceViewModel workspace)
{
    private static readonly FilePickerFileType JsonFile = new("JSON files") { Patterns = ["*.json"] };
    public static WorkspaceFileDialogs? For(Control view) =>
        TopLevel.GetTopLevel(view) is { } owner && view.DataContext is WorkspaceViewModel workspace
            ? new(owner, workspace) : null;

    public async Task CopyCurlAsync()
    {
        if (owner.Clipboard is null || await workspace.Commands.ExportCurlAsync() is not { } command)
            return;
        try
        {
            await owner.Clipboard.SetTextAsync(command);
            workspace.Status.Notice = "cURL copied, including resolved variables and credentials.";
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            workspace.Status.Notice = "Could not reach the clipboard. Try again.";
        }
    }

    public async Task CopyResponseAsync()
    {
        if (!workspace.Requests.Selected.CanCopyResponse || owner.Clipboard is null)
            return;
        try
        {
            await owner.Clipboard.SetTextAsync(workspace.Requests.Selected.ResponseDisplay);
            workspace.Status.Notice = "Response copied.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            workspace.Status.Notice = "Could not reach the clipboard. Try Save response instead.";
        }
    }
    public async Task CopyResponseHeadersAsync()
    {
        if (!workspace.Requests.Selected.HasResponse || owner.Clipboard is null)
            return;
        var text = string.Join(Environment.NewLine, workspace.Requests.Selected.ResponseHeaders.Select(h => $"{h.Key}: {h.Value}"));
        try
        {
            await owner.Clipboard.SetTextAsync(text);
            workspace.Status.Notice = "Response headers copied.";
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            workspace.Status.Notice = "Could not reach the clipboard. Try again.";
        }
    }
    public Task SaveResponseAsync()
    {
        if (workspace.Requests.Selected.Response is not { } response)
            return Task.CompletedTask;
        var bytes = ResponseExport.Bytes(response, workspace.Requests.Selected.ResponseDisplay);
        return SaveFileAsync(response.Truncated ? "Save response preview" : "Save response", ResponseExport.FileName(response),
            stream => stream.WriteAsync(bytes).AsTask(), response.Truncated);
    }
    public Task ExportAsync() => SaveTextAsync("Export Osynt collections", "osynt-collections.json", workspace.Library.ExportCollections());
    public async Task ImportAsync()
    {
        try
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Import Osynt collections",
                AllowMultiple = false,
                FileTypeFilter = [JsonFile]
            });
            if (files.Count == 0)
                return;
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            workspace.Library.ImportCollections(await reader.ReadToEndAsync());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            workspace.Status.Notice = $"Could not import this file. Choose an Osynt collection JSON export. {ex.Message}";
        }
    }
    private Task SaveTextAsync(string title, string name, string text) =>
        SaveFileAsync(title, name, stream => stream.WriteAsync(new UTF8Encoding(false).GetBytes(text)).AsTask());

    private async Task SaveFileAsync(string title, string name, Func<Stream, Task> write, bool preview = false)
    {
        try
        {
            var file = await owner.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = title,
                SuggestedFileName = name,
                DefaultExtension = Path.GetExtension(name).TrimStart('.'),
                FileTypeChoices = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? [JsonFile, FilePickerFileTypes.All] : [FilePickerFileTypes.All]
            });
            if (file is null)
                return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
                stream.SetLength(0);
            await write(stream);
            workspace.Status.Notice = preview ? $"Saved {file.Name} (preview only; the response exceeded 5 MB)." : $"Saved {file.Name}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            workspace.Status.Notice = $"Could not save this file: {ex.Message}";
        }
    }
}
