using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class ImportCurlWindow : Window
{
    private readonly RequestCommandsViewModel? _commands;
    public ImportCurlWindow() => InitializeComponent();
    public ImportCurlWindow(RequestCommandsViewModel commands) : this()
    {
        _commands = commands;
        Opened += (_, _) => CommandInput.Focus();
    }
    private void ImportClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            _commands?.ImportCurl(CommandInput.Text ?? "");
            Close();
        }
        catch (ArgumentException error)
        {
            ErrorLabel.Text = error.Message;
            ErrorLabel.IsVisible = true;
        }
    }
    private void CancelClicked(object? sender, RoutedEventArgs e) => Close();
}
