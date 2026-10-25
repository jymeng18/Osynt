using Osynt.Domain.Requests;

namespace Osynt.Application.Workspaces;

public sealed class EnvironmentService(WorkspaceSession session)
{
    public string DemoBaseUrl => session.DemoBaseUrl;
    public IReadOnlyDictionary<string, string> GetVariables() =>
        session.Read(state => new Dictionary<string, string>(state.Variables));

    public void Update(IEnumerable<RequestPair> pairs)
    {
        var values = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Key)).ToList();
        if (values.GroupBy(p => p.Key.Trim(), StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Each environment variable needs a unique name.");
        session.Commit(state =>
        {
            state.Variables = values.ToDictionary(p => p.Key.Trim(), p => p.Value);
            state.UseLocalDemo = DemoBaseUrl.Length > 0 && state.Variables.GetValueOrDefault("base_url") == DemoBaseUrl;
        });
    }
}
