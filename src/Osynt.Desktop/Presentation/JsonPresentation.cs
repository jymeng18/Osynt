using System.Text.Json;

namespace Osynt.Desktop.Presentation;

internal static class JsonPresentation
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string Format(string text)
    {
        using var document = JsonDocument.Parse(text);
        return JsonSerializer.Serialize(document.RootElement, Options);
    }
}
