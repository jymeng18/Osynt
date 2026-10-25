using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

public sealed class RequestPairViewModel : ObservableModel
{
    private bool _enabled = true;
    private string _key = "";
    private string _value = "";
    public bool Enabled
    {
        get => _enabled; set => Set(ref _enabled, value);
    }
    public string Key
    {
        get => _key; set => Set(ref _key, value);
    }
    public string Value
    {
        get => _value; set => Set(ref _value, value);
    }
    public RequestPair ToModel() => new() { Enabled = Enabled, Key = Key, Value = Value };
    public static RequestPairViewModel FromModel(RequestPair pair) => new() { Enabled = pair.Enabled, Key = pair.Key, Value = pair.Value };
}
