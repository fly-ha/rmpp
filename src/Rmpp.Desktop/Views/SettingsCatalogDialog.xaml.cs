using System.IO;
using System.Windows;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.ViewModels;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Desktop.Views;

public partial class SettingsCatalogDialog : Window
{
    public SettingsCatalogDialog() { InitializeComponent(); }

    private async void OnOpenTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && TemplateCatalogGrid.SelectedItem is TemplateCatalogEntry entry)
        {
            if (await RunAsync(() => viewModel.OpenTemplateAsync(entry.Path)))
            {
                Close();
            }
        }
    }

    private void OnTemplateDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        OnOpenTemplate(sender, new RoutedEventArgs());

    private async void OnDeleteTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || TemplateCatalogGrid.SelectedItem is not TemplateCatalogEntry entry)
        {
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            DesktopText.Format("DeleteTemplateConfirmFormat", entry.Path),
            DesktopText.Get("DeleteTemplate"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await RunAsync(() => viewModel.DeleteTemplateAsync(entry.Path));
        }
    }

    private async Task<bool> RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, DesktopText.Get("TemplateOperationFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
