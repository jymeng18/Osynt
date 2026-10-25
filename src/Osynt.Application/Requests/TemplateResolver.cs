using System.Text.RegularExpressions;

namespace Osynt.Application.Requests;

public static partial class TemplateResolver
{
    public static string Resolve(string input, IReadOnlyDictionary<string, string> variables) =>
        VariablePattern().Replace(input, match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) && !string.IsNullOrEmpty(value)
                ? value : throw new ArgumentException($"Set a value for {{{{{match.Groups[1].Value}}}}} in Environment first."));

    [GeneratedRegex(@"\{\{\s*([^{}\s]+)\s*\}\}")]
    private static partial Regex VariablePattern();
}
