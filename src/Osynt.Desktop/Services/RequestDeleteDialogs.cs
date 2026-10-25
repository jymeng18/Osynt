using Avalonia.Controls;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Services;

public sealed class RequestDeleteDialogs(Window owner, LibraryViewModel library)
{
    public async Task DeleteRequestAsync(LibraryRequest request)
    {
        var decision = await ConfirmAsync($"Delete “{request.Name}”?",
            "This request will be deleted and its open tab closed. Unsaved changes will be lost.");
        if (decision.Confirmed)
            library.DeleteRequest(request.Document.Id, decision.DontAskAgain);
    }

    public async Task DeleteCollectionAsync(LibraryCollection collection)
    {
        var decision = await ConfirmAsync($"Delete “{collection.Name}”?",
            "This folder and all its requests will be deleted. Their open tabs will close and unsaved changes will be lost.");
        if (decision.Confirmed)
            library.DeleteCollection(collection.Id, decision.DontAskAgain);
    }

    private async Task<(bool Confirmed, bool DontAskAgain)> ConfirmAsync(string heading, string description)
    {
        if (!library.ConfirmDeletion)
            return (true, false);
        var dialog = new DeleteConfirmationWindow(heading, description);
        var confirmed = await dialog.ShowDialog<bool>(owner);
        return (confirmed, dialog.SkipFutureConfirmations);
    }
}
