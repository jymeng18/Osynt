namespace Osynt.Domain.Requests;

public sealed class RequestPair
{
    public bool Enabled { get; set; } = true;
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public RequestPair Clone() => new() { Enabled = Enabled, Key = Key, Value = Value };
}
