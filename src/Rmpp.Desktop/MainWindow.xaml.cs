using System.Windows;
using System.Windows.Input;
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
        };
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
