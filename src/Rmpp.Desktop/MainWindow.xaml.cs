using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.ViewModels;
using Rmpp.Desktop.Views;

namespace Rmpp.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        InputBindings.Add(new KeyBinding(viewModel.NewDocumentCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
        PreviewKeyDown += OnWindowPreviewKeyDown;
        viewModel.DataImportRequested += OnDataImportRequested;
        viewModel.PrintSetupRequested += OnPrintSetupRequested;
        viewModel.CalibrationRequested += OnCalibrationRequested;
        viewModel.RecoveryRequested += OnRecoveryRequested;
        viewModel.SettingsRequested += OnSettingsRequested;
        Closed += (_, _) =>
        {
            viewModel.DataImportRequested -= OnDataImportRequested;
            viewModel.PrintSetupRequested -= OnPrintSetupRequested;
            viewModel.CalibrationRequested -= OnCalibrationRequested;
            viewModel.RecoveryRequested -= OnRecoveryRequested;
            viewModel.SettingsRequested -= OnSettingsRequested;
            PreviewKeyDown -= OnWindowPreviewKeyDown;
        };
    }

    private async void OnOpenTemplate(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = DesktopText.Get("OpenTemplate"),
            Filter = DesktopText.Get("TemplateFileFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await RunTemplateActionAsync(() => viewModel.OpenTemplateAsync(dialog.FileName));
    }

    private async void OnSaveTemplate(object sender, RoutedEventArgs e) => await SaveTemplateAsync(saveAs: false);

    private async void OnSaveTemplateAs(object sender, RoutedEventArgs e) => await SaveTemplateAsync(saveAs: true);

    private async void OnDeleteTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.ActiveDocument?.OriginalTemplatePath is not { } path)
        {
            MessageBox.Show(this, DesktopText.Get("TemplateMustBeSavedBeforeDelete"), DesktopText.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            DesktopText.Format("DeleteTemplateConfirmFormat", path),
            DesktopText.Get("DeleteTemplate"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await RunTemplateActionAsync(() => viewModel.DeleteTemplateAsync(path));
        }
    }

    private async Task SaveTemplateAsync(bool saveAs)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.ActiveDocument is null)
        {
            return;
        }

        string? path = saveAs ? null : viewModel.ActiveDocument.OriginalTemplatePath;
        if (path is null)
        {
            SaveFileDialog dialog = new()
            {
                Title = DesktopText.Get("SaveTemplateAs"),
                Filter = DesktopText.Get("TemplateFileFilter"),
                DefaultExt = ".rmpp",
                AddExtension = true,
                FileName = viewModel.ActiveDocument.DisplayTitle,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            path = dialog.FileName;
        }

        await RunTemplateActionAsync(() => viewModel.SaveActiveTemplateAsync(path));
    }

    private async Task RunTemplateActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, DesktopText.Get("TemplateOperationFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (e.Key == Key.O)
        {
            OnOpenTemplate(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.S)
        {
            await SaveTemplateAsync(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void OnDataImportRequested(object? sender, DataImportViewModel viewModel)
    {
        using (viewModel) new DataImportDialog { Owner = this, DataContext = viewModel }.ShowDialog();
    }

    private void OnPrintSetupRequested(object? sender, PrintSetupViewModel viewModel) =>
        new PrintSetupDialog { Owner = this, DataContext = viewModel }.ShowDialog();

    private void OnCalibrationRequested(object? sender, CalibrationWizardViewModel viewModel) =>
        new CalibrationWizard { Owner = this, DataContext = viewModel }.ShowDialog();

    private void OnRecoveryRequested(object? sender, RecoveryDialogViewModel viewModel) =>
        new RecoveryDialog { Owner = this, DataContext = viewModel }.ShowDialog();

    private void OnSettingsRequested(object? sender, EventArgs e) =>
        new SettingsCatalogDialog { Owner = this, DataContext = DataContext }.ShowDialog();
}
