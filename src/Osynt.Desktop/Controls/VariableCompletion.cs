using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Controls;

/// <summary>Environment variable completion shared by request editor text fields.</summary>
internal sealed class VariableCompletion
{
    private readonly Control owner;
    private readonly ListBox suggestions;
    private readonly Popup popup;
    private TextBox? input;
    private bool accepting;
    private int start;
    private int end;

    public VariableCompletion(Control owner)
    {
        this.owner = owner;
        popup = owner.FindControl<Popup>("VariablePopup")!;
        suggestions = owner.FindControl<ListBox>("VariableSuggestions")!;
        var surface = owner.FindControl<Border>("VariablePopupSurface")!;
        surface.Opacity = 0;
        popup.Closed += (_, _) => surface.Opacity = 0;
        popup.Opened += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => surface.Opacity = 1,
                Avalonia.Threading.DispatcherPriority.Render);
        };
        owner.AddHandler(InputElement.GotFocusEvent, OnFocus);
        owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        suggestions.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (suggestions.SelectedItem is string) Accept();
        }, RoutingStrategies.Bubble);
        owner.DetachedFromVisualTree += (_, _) => Detach();
    }

    private void OnFocus(object? sender, GotFocusEventArgs e)
    {
        if (e.Source is not TextBox field || field == input) return;
        Detach();
        input = field;
        popup.PlacementTarget = field;
        field.PropertyChanged += OnPropertyChanged;
        field.LostFocus += OnLostFocus;
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        // Let pointer selection finish before dismissing the popup.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (input?.IsKeyboardFocusWithin != true && !suggestions.IsKeyboardFocusWithin)
                popup.IsOpen = false;
        });
    }

    private void Detach()
    {
        popup.IsOpen = false;
        if (input is null) return;
        input.PropertyChanged -= OnPropertyChanged;
        input.LostFocus -= OnLostFocus;
        input = null;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!accepting && (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty))
            Refresh();
    }

    private void Refresh()
    {
        if (input is null || !input.IsKeyboardFocusWithin || owner.DataContext is not WorkspaceViewModel workspace)
        { popup.IsOpen = false; return; }
        var text = input.Text ?? "";
        var caret = Math.Clamp(input.CaretIndex, 0, text.Length);
        start = text[..caret].LastIndexOf("{{", StringComparison.Ordinal);
        if (start < 0) { popup.IsOpen = false; return; }
        var prefix = text[(start + 2)..caret].TrimStart();
        if (prefix.Any(c => char.IsWhiteSpace(c) || c is '{' or '}')) { popup.IsOpen = false; return; }
        end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('{' or '}')) end++;
        if (text.AsSpan(end).StartsWith("}}")) end += 2;
        else end = caret;
        var matches = workspace.Environment.Variables.Keys
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (matches.Length == 0) { popup.IsOpen = false; return; }
        suggestions.ItemsSource = matches;
        suggestions.SelectedIndex = 0;
        popup.IsOpen = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!popup.IsOpen || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key is Key.Down or Key.Up)
        {
            var count = suggestions.ItemCount;
            suggestions.SelectedIndex = (suggestions.SelectedIndex + (e.Key == Key.Down ? 1 : count - 1)) % count;
            suggestions.ScrollIntoView(suggestions.SelectedItem!);
        }
        else if (e.Key is Key.Enter or Key.Tab) Accept();
        else if (e.Key == Key.Escape) popup.IsOpen = false;
        else return;
        e.Handled = true;
    }

    private void Accept()
    {
        if (input is null || suggestions.SelectedItem is not string name) return;
        accepting = true;
        try
        {
            input.SelectionStart = start;
            input.SelectionEnd = end;
            input.SelectedText = "{{" + name + "}}";
            input.CaretIndex = start + name.Length + 4;
            popup.IsOpen = false;
            input.Focus();
        }
        finally { accepting = false; }
    }
}
