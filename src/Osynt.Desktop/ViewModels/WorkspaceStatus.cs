namespace Osynt.Desktop.ViewModels;

public sealed class WorkspaceStatus : ObservableModel
{
    private string _notice = "Ready";
    public string Notice
    {
        get => _notice;
        set => Set(ref _notice, value);
    }

    public bool TrySave(Action save, string? successMessage = null)
    {
        try
        {
            save();
            if (successMessage is not null)
                Notice = successMessage;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportSaveFailure(ex.Message);
            return false;
        }
    }

    public void ReportSaveFailure(string error) => Notice = $"Could not save your workspace: {error}";
}
