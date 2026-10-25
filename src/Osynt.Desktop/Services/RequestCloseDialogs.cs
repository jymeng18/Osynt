using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Services;

public sealed class RequestCloseDialogs(Window owner, WorkspaceViewModel workspace)
{
    public async Task CloseRequestAsync(RequestTab tab)
    {
        if (tab.IsDirty)
        {
            var choice = await AskToSaveAsync($"Save “{tab.Document.Name}”?", "Give this request a home before closing it.");
            if (choice == "cancel")
                return;
            if (choice == "save")
            {
                workspace.Requests.Selected = tab;
                workspace.Requests.SaveSelected();
                if (tab.IsDirty)
                    return;
            }
        }
        workspace.Requests.Discard(tab);
    }
    public async Task<bool> CanCloseWorkspaceAsync()
    {
        var choice = await AskToSaveAsync("Tuck your requests away?", "Some requests have unsaved changes. Save them before heading out?");
        if (choice == "cancel")
            return false;
        if (choice == "save")
        {
            var selected = workspace.Requests.Selected;
            foreach (var tab in workspace.Requests.Tabs.Where(t => t.IsDirty).ToArray())
            {
                workspace.Requests.Selected = tab;
                workspace.Requests.SaveSelected();
                if (tab.IsDirty)
                {
                    workspace.Requests.Selected = selected;
                    return false;
                }
            }
        }
        return true;
    }
    private async Task<string> AskToSaveAsync(string title, string description)
    {
        var dialog = new Window { Title = title, Width = 490, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(28), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = Brush.Parse("#8D9680"), TextWrapping = TextWrapping.Wrap, LineHeight = 20 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        foreach (var (label, value) in new[] { ("Keep editing", "cancel"), ("Discard", "discard"), ("Save & close", "save") })
        {
            var button = new Button { Content = label };
            if (value == "save")
                button.Classes.Add("primary");
            button.Click += (_, _) => dialog.Close(value);
            buttons.Children.Add(button);
        }
        panel.Children.Add(buttons);
        dialog.Content = panel;
        return await dialog.ShowDialog<string?>(owner) ?? "cancel";
    }
}
