namespace Osynt.Domain.Requests;

public static class RequestBodyModes
{
    public const string Json = "JSON";
    public const string Text = "Text";
    public const string Form = "Form";
    public const string None = "None";
    public static IReadOnlyList<string> All { get; } = [Json, Text, Form, None];
}
