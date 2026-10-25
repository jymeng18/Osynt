using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Osynt.Desktop.Controls;

public sealed partial class CodeViewer : UserControl
{
    public static readonly StyledProperty<string> CodeProperty = AvaloniaProperty.Register<CodeViewer, string>(nameof(Code), "");
    public static readonly StyledProperty<bool> HighlightProperty = AvaloniaProperty.Register<CodeViewer, bool>(nameof(Highlight), true);
    public string Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public bool Highlight { get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }
    private readonly SelectableTextBlock _text = new() { FontFamily = new("Cascadia Code, DejaVu Sans Mono, Consolas, monospace"), FontSize = 12, LineHeight = 21, Foreground = Brush.Parse("#72756B"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly TextBlock _numbers = new() { FontFamily = new("Cascadia Code, DejaVu Sans Mono, Consolas, monospace"), FontSize = 12, LineHeight = 21, Foreground = Brush.Parse("#BCBEAE"), TextAlignment = TextAlignment.Right, Margin = new(0, 0, 22, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    static CodeViewer()
    {
        CodeProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.UpdateCode());
        HighlightProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.UpdateCode());
    }
    public CodeViewer()
    {
        var grid = new Grid { ColumnDefinitions = new("46,*"), Margin = new(0, 14, 24, 20) };
        grid.Children.Add(_numbers); Grid.SetColumn(_text, 1); grid.Children.Add(_text);
        Content = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }
    private void UpdateCode()
    {
        // Very large or non-JSON responses use a plain selectable surface to keep the UI responsive.
        _text.Inlines?.Clear();
        if (!Highlight || Code.Length > 200_000 || !(Code.TrimStart().StartsWith('{') || Code.TrimStart().StartsWith('['))) _text.Text = Code;
        else
        {
            _text.Text = "";
            var position = 0;
            foreach (Match match in JsonToken().Matches(Code))
            {
                if (match.Index > position) _text.Inlines!.Add(new Run(Code[position..match.Index]));
                var color = match.Value.StartsWith('"') ? match.Groups[1].Success ? "#8C7F94" : "#69844E" : "#B58A56";
                _text.Inlines!.Add(new Run(match.Value) { Foreground = Brush.Parse(color) });
                position = match.Index + match.Length;
            }
            if (position < Code.Length) _text.Inlines!.Add(new Run(Code[position..]));
        }
        var count = Code.Length == 0 ? 0 : Code.Count(c => c == '\n') + 1;
        _numbers.Text = count <= 10000 ? string.Join('\n', Enumerable.Range(1, count)) : "";
    }
    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"(\\s*:)?|\\b(?:true|false|null|-?\\d+(?:\\.\\d+)?)\\b")]
    private static partial Regex JsonToken();
}
