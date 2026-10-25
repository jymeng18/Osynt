using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Osynt.Desktop;

public partial class DeleteConfirmationWindow : Window
{
    public DeleteConfirmationWindow() => InitializeComponent();

    public bool SkipFutureConfirmations => DontAskAgain.IsChecked == true;

    public DeleteConfirmationWindow(string heading, string description) : this()
    {
        Title = "Confirm deletion";
        Heading.Text = heading;
        Description.Text = description;
    }

    private void DeleteClicked(object? sender, RoutedEventArgs e) => Close(true);
    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
