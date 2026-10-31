using System.Text.Json;
using Osynt.Domain.Requests;
using Osynt.Domain.Workflows;

namespace Osynt.Application.Workflows;

public static class ResponseFields
{
    public static IReadOnlyList<string> List(ResponseData response)
    {
        using var json = Parse(response);
        var fields = new List<string>();
        void Walk(JsonElement value, string path)
        {
            if (value.ValueKind == JsonValueKind.Object)
                foreach (var property in value.EnumerateObject())
                    Walk(property.Value, path + "/" + property.Name.Replace("~", "~0").Replace("/", "~1"));
            else if (value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    Walk(item, path + "/" + index++);
            }
            else
                fields.Add(path);
        }
        Walk(json.RootElement, "");
        return fields;
    }

    public sealed record Field(string Pointer, string Value, string SuggestedName);

    public static IReadOnlyList<Field> Describe(ResponseData response)
    {
        using var json = Parse(response);
        var fields = new List<Field>();
        void Walk(JsonElement element, string pointer, string name)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                    Walk(property.Value, pointer + "/" + property.Name.Replace("~", "~0").Replace("/", "~1"), property.Name);
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Walk(item, pointer + "/" + index++, name);
            }
            else if (element.ValueKind != JsonValueKind.Null)
            {
                var value = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
                if (value.Length == 0)
                    return;
                var suggested = System.Text.RegularExpressions.Regex.Replace(name, @"[^\p{L}\p{N}_-]", "_");
                fields.Add(new Field(pointer, value, suggested.Length == 0 ? "response_value" : suggested));
            }
        }
        Walk(json.RootElement, "", "response_value");
        return fields;
    }

    public static Dictionary<string, string> Extract(ResponseData response, IReadOnlyList<ResponseCapture> captures)
    {
        using var json = Parse(response);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var capture in captures)
        {
            Validate(capture);
            var value = json.RootElement;
            foreach (var segment in capture.Pointer.Split('/').Skip(1))
            {
                var key = segment.Replace("~1", "/").Replace("~0", "~");
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var child))
                    value = child;
                else if (value.ValueKind == JsonValueKind.Array && int.TryParse(key, out var index) && index >= 0 && index < value.GetArrayLength())
                    value = value[index];
                else
                    throw new ArgumentException($"Response field {capture.Pointer} is missing. No variables were captured for this step.");
            }
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array)
                throw new ArgumentException($"Response field {capture.Pointer} must be a non-null scalar value.");
            var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
            if (text.Length == 0)
                throw new ArgumentException($"Response field {capture.Pointer} is empty.");
            if (!values.TryAdd(capture.Variable, text))
                throw new ArgumentException("Capture variable names must be unique within a step.");
        }
        return values;
    }

    public static void Validate(ResponseCapture capture)
    {
        if (string.IsNullOrWhiteSpace(capture.Variable) || capture.Variable.Any(c => char.IsWhiteSpace(c) || c is '{' or '}'))
            throw new ArgumentException("Variable names cannot contain spaces or braces.");
        if (capture.Pointer.Length > 0 && !capture.Pointer.StartsWith('/'))
            throw new ArgumentException("Select a JSON response field first.");
    }

    private static JsonDocument Parse(ResponseData response)
    {
        if (response.Truncated || response.IsBinary)
            throw new ArgumentException("Captures require a complete JSON response.");
        try
        {
            return JsonDocument.Parse(response.Body);
        }
        catch (JsonException) { throw new ArgumentException("The response is not valid JSON."); }
    }
}
