using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Osynt.Core;

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RequestPair : ObservableModel
{
    private bool _enabled = true;
    private string _key = "";
    private string _value = "";
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string Key { get => _key; set => Set(ref _key, value); }
    public string Value { get => _value; set => Set(ref _value, value); }
    public RequestPair Clone() => new() { Enabled = Enabled, Key = Key, Value = Value };
}

public sealed class RequestDocument : ObservableModel
{
    private string _name = "Untitled request";
    private string _method = "GET";
    private string _url = "{{base_url}}/v1/harvest";
    private string _body = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Method { get => _method; set => Set(ref _method, value); }
    public string Url { get => _url; set => Set(ref _url, value); }
    public string Body { get => _body; set => Set(ref _body, value); }
    public ObservableCollection<RequestPair> Parameters { get; set; } = [];
    public ObservableCollection<RequestPair> Headers { get; set; } = [];

    public RequestDocument Clone(bool newId = false) => new()
    {
        Id = newId ? Guid.NewGuid() : Id, Name = Name, Method = Method, Url = Url, Body = Body,
        Parameters = new(Parameters.Select(p => p.Clone())),
        Headers = new(Headers.Select(p => p.Clone()))
    };
}

public sealed class RequestCollection
{
    public string Name { get; set; } = "My collection";
    public List<RequestDocument> Requests { get; set; } = [];
}

public sealed class HistoryEntry
{
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.Now;
    public RequestDocument Request { get; set; } = new();
    public int StatusCode { get; set; }
    public long ElapsedMilliseconds { get; set; }
    [JsonIgnore] public string TimeLabel => SentAt.ToLocalTime().ToString("HH:mm");
    [JsonIgnore] public string StatusLabel => StatusCode == 0 ? "ERR" : StatusCode.ToString();
}

public sealed class WorkspaceState
{
    public bool UseLocalDemo { get; set; } = true;
    public List<RequestCollection> Collections { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = new()
    {
        ["base_url"] = "", ["season"] = "spring"
    };

    public static WorkspaceState CreateDefault() => new()
    {
        Collections =
        [
            new()
            {
                Name = "The little farm",
                Requests =
                [
                    new()
                    {
                        Name = "Get seasonal crops", Url = "{{base_url}}/v1/harvest",
                        Parameters = [new() { Key = "season", Value = "{{season}}" }],
                        Headers = [new() { Key = "Accept", Value = "application/json" }]
                    },
                    new() { Name = "Meet the villagers", Url = "{{base_url}}/v1/villagers" },
                    new()
                    {
                        Name = "Plant something new", Method = "POST", Url = "{{base_url}}/v1/plant",
                        Body = "{\n  \"crop\": \"strawberry\",\n  \"quantity\": 12\n}",
                        Headers = [new() { Key = "Content-Type", Value = "application/json" }]
                    },
                    new() { Name = "Check on the farm", Url = "{{base_url}}/v1/health" }
                ]
            },
            new()
            {
                Name = "Out in the world",
                Requests = [new() { Name = "Hello, JSONPlaceholder", Url = "https://jsonplaceholder.typicode.com/posts/1" }]
            }
        ]
    };
}

public sealed record ResponseData(int StatusCode, string Reason, long ElapsedMilliseconds,
    long SizeBytes, string Body, IReadOnlyList<KeyValuePair<string, string>> Headers, bool Truncated = false)
{
    public bool IsSuccess => StatusCode is >= 200 and < 400;
}
