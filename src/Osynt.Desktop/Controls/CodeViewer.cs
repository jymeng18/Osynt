using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.VisualTree;

namespace Osynt.Desktop.Controls;

public sealed partial class CodeViewer : UserControl
{
    public static readonly StyledProperty<string> CodeProperty = AvaloniaProperty.Register<CodeViewer, string>(nameof(Code), "");
    public static readonly StyledProperty<bool> HighlightProperty = AvaloniaProperty.Register<CodeViewer, bool>(nameof(Highlight), true);
    public string Code
    {
        get => GetValue(CodeProperty); set => SetValue(CodeProperty, value);
    }
    public bool Highlight
    {
        get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value);
    }
    private readonly SelectableTextBlock _text = new() { FontFamily = new("Cascadia Code, DejaVu Sans Mono, Consolas, monospace"), FontSize = 12, LineHeight = 21, Foreground = Brush.Parse("#515C49"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly TextBlock _numbers = new() { FontFamily = new("Cascadia Code, DejaVu Sans Mono, Consolas, monospace"), FontSize = 12, LineHeight = 21, Foreground = Brush.Parse("#7A826F"), TextAlignment = TextAlignment.Right, Margin = new(0, 0, 22, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly TextBox _search = new() { Name = "ResponseSearchInput", Watermark = "Find in response…", MinWidth = 140, FontSize = 11, Padding = new(8, 4) };
    private readonly TextBlock _findStatus = new() { Name = "ResponseFindStatus", FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new(8, 0) };
    private readonly Grid _findBar;
    private int _matchCount;
    private int _matchNumber;
    private int _matchIndex = -1;
    private int _lastMatch = -1;
    static CodeViewer()
    {
        CodeProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.UpdateCode());
        HighlightProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.UpdateCode());
    }
    public CodeViewer()
    {
        var grid = new Grid { ColumnDefinitions = new("46,*"), Margin = new(0, 14, 24, 20) };
        grid.Children.Add(_numbers);
        Grid.SetColumn(_text, 1);
        grid.Children.Add(_text);
        _findBar = new Grid { ColumnDefinitions = new("*,Auto,Auto,Auto,Auto"), Margin = new(10, 6), IsVisible = false };
        AutomationProperties.SetName(_search, "Find in response");
        _findBar.Children.Add(_search);
        Grid.SetColumn(_findStatus, 1);
        _findBar.Children.Add(_findStatus);
        AddFindButton("Previous", 2, () => MoveMatch(-1));
        AddFindButton("Next", 3, () => MoveMatch(1));
        AddFindButton("Close", 4, () => { _findBar.IsVisible = false; _text.Focus(); });
        _search.TextChanged += (_, _) => UpdateMatches();
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                MoveMatch(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _findBar.IsVisible = false;
                _text.Focus();
                e.Handled = true;
            }
        };
        var layout = new Grid { RowDefinitions = new("Auto,*") };
        layout.Children.Add(_findBar);
        var scroll = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        Content = layout;
    }
    public void ShowFind()
    {
        _findBar.IsVisible = true;
        _search.Focus();
        _search.SelectAll();
    }
    private void AddFindButton(string label, int column, Action action)
    {
        var button = new Button { Content = label, FontSize = 10, Padding = new(7, 4) };
        AutomationProperties.SetName(button, label == "Close" ? "Close response search" : label + " response match");
        button.Click += (_, _) => action();
        Grid.SetColumn(button, column);
        _findBar.Children.Add(button);
    }
    private void UpdateMatches()
    {
        _matchCount = 0;
        _matchNumber = 0;
        _matchIndex = -1;
        _lastMatch = -1;
        var query = _search.Text ?? "";
        if (query.Length > 0)
            for (var start = 0; start <= Code.Length - query.Length;)
            {
                var index = Code.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    break;
                if (_matchIndex < 0)
                    _matchIndex = index;
                _lastMatch = index;
                _matchCount++;
                start = index + query.Length;
            }
        _matchNumber = _matchCount > 0 ? 1 : 0;
        ShowMatch();
    }
    private void MoveMatch(int direction)
    {
        var query = _search.Text ?? "";
        if (_matchCount == 0 || query.Length == 0)
            return;
        if (direction > 0)
        {
            var next = Code.IndexOf(query, _matchIndex + query.Length, StringComparison.OrdinalIgnoreCase);
            _matchIndex = next < 0 ? Code.IndexOf(query, StringComparison.OrdinalIgnoreCase) : next;
            _matchNumber = next < 0 ? 1 : _matchNumber + 1;
        }
        else if (_matchNumber == 1)
        {
            _matchIndex = _lastMatch;
            _matchNumber = _matchCount;
        }
        else
        {
            var previous = -1;
            for (var start = 0; start < _matchIndex;)
            {
                var index = Code.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0 || index >= _matchIndex)
                    break;
                previous = index;
                start = index + query.Length;
            }
            _matchIndex = previous;
            _matchNumber--;
        }
        ShowMatch();
    }
    private void ShowMatch()
    {
        var query = _search.Text ?? "";
        _findStatus.Text = query.Length == 0 ? "" : _matchCount == 0 ? "No matches" : $"{_matchNumber} of {_matchCount}";
        _text.SelectionStart = Math.Max(0, _matchIndex);
        _text.SelectionEnd = _matchIndex < 0 ? 0 : _matchIndex + query.Length;
        if (_matchIndex >= 0 && _text.GetVisualRoot() is not null)
            _text.BringIntoView(_text.TextLayout.HitTestTextPosition(_matchIndex));
    }
    private void UpdateCode()
    {
        // Very large or non-JSON responses use a plain selectable surface to keep the UI responsive.
        _text.Inlines?.Clear();
        if (!Highlight || Code.Length > 200_000 || !(Code.TrimStart().StartsWith('{') || Code.TrimStart().StartsWith('[')))
            _text.Text = Code;
        else
        {
            _text.Text = "";
            var position = 0;
            foreach (Match match in JsonToken().Matches(Code))
            {
                if (match.Index > position)
                    _text.Inlines!.Add(new Run(Code[position..match.Index]));
                var color = match.Value.StartsWith('"') ? match.Groups[1].Success ? "#776383" : "#527336" : "#94602F";
                _text.Inlines!.Add(new Run(match.Value) { Foreground = Brush.Parse(color) });
                position = match.Index + match.Length;
            }
            if (position < Code.Length)
                _text.Inlines!.Add(new Run(Code[position..]));
        }
        var count = Code.Length == 0 ? 0 : Code.Count(c => c == '\n') + 1;
        _numbers.Text = count <= 10000 ? string.Join('\n', Enumerable.Range(1, count)) : "";
        UpdateMatches();
    }
    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"(\\s*:)?|\\b(?:true|false|null|-?\\d+(?:\\.\\d+)?)\\b")]
    private static partial Regex JsonToken();
}
