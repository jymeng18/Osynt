using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Controls;

public sealed record VariableSuggestion(string Name, string Source, string Preview)
{
    public static VariableSuggestion Environment(string name, string value) => new(name, "Environment", PreviewValue(name, value));
    public static string PreviewValue(string name, string value) =>
        new[] { "token", "password", "secret", "key", "credential" }.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))
            ? (value.Length == 0 ? "No value set" : "••••••••")
            : value.Length == 0 ? "No value set" : value.Length > 100 ? value[..100] + "…" : value.Replace('\n', ' ');
}

/// <summary>Shared completion for request fields and the workflow inspector.</summary>
internal sealed class VariableCompletion
{
    private readonly Control owner;
    private readonly ListBox suggestions;
    private readonly Popup popup;
    private readonly TextBlock empty;
    private readonly Func<IEnumerable<VariableSuggestion>> getVariables;
    private readonly bool markedFieldsOnly;
    private TextBox? input;
    private bool accepting;
    private bool queued;
    private bool dismissed;
    private int start;
    private int end;

    public VariableCompletion(Control owner, Func<IEnumerable<VariableSuggestion>>? variables = null, bool markedFieldsOnly = false)
    {
        this.owner = owner;
        this.markedFieldsOnly = markedFieldsOnly;
        getVariables = variables ?? (() => owner.DataContext is WorkspaceViewModel workspace
            ? workspace.Environment.Variables.Select(p => VariableSuggestion.Environment(p.Key, p.Value)) : []);
        var view = owner.FindControl<VariableCompletionView>("CompletionView")!;
        popup = view.FindControl<Popup>("VariablePopup")!;
        suggestions = view.FindControl<ListBox>("VariableSuggestions")!;
        empty = view.FindControl<TextBlock>("EmptySuggestions")!;
        owner.AddHandler(InputElement.GotFocusEvent, OnFocus);
        owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        suggestions.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(suggestions).Properties.IsLeftButtonPressed)
                return;
            var item = (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
            if (item?.DataContext is not VariableSuggestion choice)
                return;
            suggestions.SelectedItem = choice;
            Accept();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        owner.DetachedFromVisualTree += (_, _) => Detach();
    }

    private void OnFocus(object? sender, GotFocusEventArgs e)
    {
        if (e.Source is not TextBox field || field == input)
            return;
        Detach();
        if (markedFieldsOnly && !field.Classes.Contains("variables"))
            return;
        input = field;
        popup.PlacementTarget = field;
        field.PropertyChanged += OnPropertyChanged;
        field.LostFocus += OnLostFocus;
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (input?.IsKeyboardFocusWithin != true && !suggestions.IsKeyboardFocusWithin)
            popup.IsOpen = false;
    });

    private void Detach()
    {
        popup.IsOpen = false;
        if (input is null)
            return;
        input.PropertyChanged -= OnPropertyChanged;
        input.LostFocus -= OnLostFocus;
        input = null;
        dismissed = false;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (accepting)
            return;
        if (e.Property == TextBox.TextProperty)
            dismissed = false;
        if (e.Property != TextBox.TextProperty && e.Property != TextBox.CaretIndexProperty)
            return;
        if (queued)
            return;
        queued = true;
        Dispatcher.UIThread.Post(() => { queued = false; Refresh(); }, DispatcherPriority.Input);
    }

    private void Refresh()
    {
        if (dismissed || input is null || !input.IsKeyboardFocusWithin || input.SelectionStart != input.SelectionEnd)
        {
            popup.IsOpen = false;
            return;
        }
        var text = input.Text ?? "";
        var caret = Math.Clamp(input.CaretIndex, 0, text.Length);
        start = text[..caret].LastIndexOf("{{", StringComparison.Ordinal);
        if (start < 0)
        {
            popup.IsOpen = false;
            return;
        }
        var prefix = text[(start + 2)..caret].TrimStart();
        if (prefix.Any(c => char.IsWhiteSpace(c) || c is '{' or '}'))
        {
            popup.IsOpen = false;
            return;
        }
        var closing = text.IndexOf('}', caret);
        var token = closing >= 0 ? text[(start + 2)..closing].Trim() : "";
        if (closing >= 0 && token.All(c => !char.IsWhiteSpace(c) && c is not ('{' or '}')))
        {
            end = closing + 1;
            if (end < text.Length && text[end] == '}')
                end++;
        }
        else
        {
            end = caret;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '-' or '.'))
                end++;
        }
        var selectedName = (suggestions.SelectedItem as VariableSuggestion)?.Name;
        var matches = getVariables().Where(v => v.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).DistinctBy(v => v.Name).ToArray();
        empty.IsVisible = matches.Length == 0;
        suggestions.IsVisible = matches.Length > 0;
        suggestions.ItemsSource = matches;
        suggestions.SelectedItem = matches.FirstOrDefault(v => v.Name == selectedName) ?? matches.FirstOrDefault();
        // Place the menu beside the caret, including in multiline JSON and scrolled fields.
        var presenter = input.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is not null)
        {
            var caretPoint = presenter.TranslatePoint(presenter.TextLayout.HitTestTextPosition(input.CaretIndex).BottomLeft, input);
            if (caretPoint is { } point)
                popup.PlacementRect = new Rect(Math.Clamp(point.X, 0, Math.Max(0, input.Bounds.Width - 30)), point.Y, 1, 1);
        }
        popup.IsOpen = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (input is { IsKeyboardFocusWithin: true } && e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            dismissed = false;
            var text = input.Text ?? "";
            var caret = input.CaretIndex;
            var open = text[..Math.Clamp(caret, 0, text.Length)].LastIndexOf("{{", StringComparison.Ordinal);
            if (open < 0 || text[(open + 2)..caret].Any(c => char.IsWhiteSpace(c) || c is '{' or '}'))
            {
                accepting = true;
                var insertion = input.SelectionStart == input.SelectionEnd ? caret : Math.Min(input.SelectionStart, input.SelectionEnd);
                input.SelectedText = "{{}}";
                input.CaretIndex = insertion + 2;
                accepting = false;
            }
            Refresh();
            e.Handled = true;
            return;
        }
        if (queued && popup.IsOpen)
            Refresh();
        if (!popup.IsOpen || e.KeyModifiers != KeyModifiers.None)
            return;
        if (suggestions.ItemCount == 0 && e.Key != Key.Escape)
            return;
        if (e.Key is Key.Down or Key.Up)
        {
            suggestions.SelectedIndex = (suggestions.SelectedIndex + (e.Key == Key.Down ? 1 : suggestions.ItemCount - 1)) % suggestions.ItemCount;
            suggestions.ScrollIntoView(suggestions.SelectedItem!);
        }
        else if (e.Key is Key.Enter or Key.Tab)
            Accept();
        else if (e.Key == Key.Escape)
        {
            dismissed = true;
            popup.IsOpen = false;
        }
        else
            return;
        e.Handled = true;
    }

    private void Accept()
    {
        if (input is null || suggestions.SelectedItem is not VariableSuggestion choice)
            return;
        accepting = true;
        try
        {
            input.SelectionStart = start;
            input.SelectionEnd = end;
            input.SelectedText = "{{" + choice.Name + "}}";
            input.CaretIndex = start + choice.Name.Length + 4;
            popup.IsOpen = false;
            dismissed = true;
            input.Focus();
        }
        finally { accepting = false; }
    }
}
