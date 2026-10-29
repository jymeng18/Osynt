namespace Osynt.Domain.Requests;

public sealed class RequestAuthentication
{
    public const string None = "None";
    public const string Bearer = "Bearer";
    public const string Basic = "Basic";
    public static IReadOnlyList<string> Modes { get; } = [None, Bearer, Basic];

    public string Mode { get; set; } = None;
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public RequestAuthentication Clone() => new() { Mode = Mode, Token = Token, Username = Username, Password = Password };
}
