using Osynt.Application.Abstractions;

namespace Osynt.Desktop.ViewModels;

public sealed class RequestCommandsViewModel(ICurlRequestSerializer serializer, RequestTabsViewModel requests,
    EnvironmentViewModel environment, WorkspaceStatus status)
{
    public void ImportCurl(string command)
    {
        var request = serializer.Deserialize(command);
        requests.OpenImported(request);
        status.Notice = "cURL imported as an unsaved request. Review it before sending.";
    }

    public async Task<string?> ExportCurlAsync()
    {
        try
        {
            return await serializer.SerializeAsync(requests.Selected.Document.Clone(), environment.Variables);
        }
        catch (ArgumentException error)
        {
            status.Notice = "Could not copy cURL: " + error.Message;
            return null;
        }
    }
}
