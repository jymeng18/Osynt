using Osynt.Application.Workspaces;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

public sealed class EnvironmentViewModel(EnvironmentService environment, WorkspaceStatus status) : ObservableModel
{
    public IReadOnlyDictionary<string, string> Variables => environment.GetVariables();
    public string DemoBaseUrl => environment.DemoBaseUrl;
    public bool DemoAvailable => DemoBaseUrl.Length > 0;
    public string Label => Variables.GetValueOrDefault("base_url") == DemoBaseUrl && DemoAvailable
        ? "Local demo" : "Custom environment";

    public void Refresh()
    {
        Raise(nameof(Variables));
        Raise(nameof(Label));
    }

    public bool UpdateVariables(IEnumerable<RequestPair> pairs)
    {
        if (!status.TrySave(() => environment.Update(pairs), "Environment saved."))
            return false;
        Raise(nameof(Variables));
        Raise(nameof(Label));
        return true;
    }
}
