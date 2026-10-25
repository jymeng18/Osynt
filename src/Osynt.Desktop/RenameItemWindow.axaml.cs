using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Osynt.Desktop;

public partial class RenameItemWindow : Window
{
    private readonly Func<string, bool>? _save;

    public RenameItemWindow() => InitializeComponent();

    public RenameItemWindow(string title, string name, Func<string, bool> save) : this()
    {
        _save = save;
        Title = title;
        Heading.Text = title;
        NameInput.Text = name;
        Opened += (_, _) => { NameInput.Focus(); NameInput.SelectAll(); };
    }

    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_save?.Invoke(NameInput.Text ?? "") == true)
            {
                Close();
                return;
            }
            ShowError("Could not save the name.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close();
}
