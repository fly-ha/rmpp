using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.Services;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Templates;
using Rmpp.Printing.Windows.Printers;

namespace Rmpp.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private DocumentTabViewModel? activeDocument;
    private readonly IDataSourceReader[] dataReaders;
    private readonly PrintJobPlanner printJobPlanner;
    private readonly PrintJobValidator printJobValidator;
    private readonly PrintPreviewService printPreviewService;
    private readonly WindowsPrinterCatalog? printerCatalog;
    private readonly IPrinterService? printerService;
    private readonly IRenderExporter? renderExporter;
    private readonly CalibrationProfileRepository? calibrationRepository;
    private readonly RecoveryCoordinator? recoveryCoordinator;
    private readonly LocalHelpService? helpService;

    public MainWindowViewModel(
        IEnumerable<IDataSourceReader>? dataReaders = null,
        PrintJobPlanner? printJobPlanner = null,
        PrintJobValidator? printJobValidator = null,
        PrintPreviewService? printPreviewService = null,
        WindowsPrinterCatalog? printerCatalog = null,
        IPrinterService? printerService = null,
        IRenderExporter? renderExporter = null,
        CalibrationProfileRepository? calibrationRepository = null,
        RecoveryCoordinator? recoveryCoordinator = null,
        SettingsViewModel? settings = null,
        TemplateCatalogViewModel? catalog = null,
        LocalHelpService? helpService = null)
    {
        this.dataReaders = (dataReaders ?? Array.Empty<IDataSourceReader>()).ToArray();
        this.printJobPlanner = printJobPlanner ?? new PrintJobPlanner();
        this.printJobValidator = printJobValidator ?? new PrintJobValidator();
        this.printPreviewService = printPreviewService ?? new PrintPreviewService();
        this.printerCatalog = printerCatalog;
        this.printerService = printerService;
        this.renderExporter = renderExporter;
        this.calibrationRepository = calibrationRepository;
        this.recoveryCoordinator = recoveryCoordinator;
        this.helpService = helpService;
        Settings = settings;
        Catalog = catalog;
        NewDocumentCommand = new RelayCommand(CreateDocument);
        CloseActiveDocumentCommand = new RelayCommand(CloseActiveDocument, () => ActiveDocument is not null);
        ImportDataCommand = new RelayCommand(ImportData, () => ActiveDocument is not null && this.dataReaders.Length > 0);
        PrintSetupCommand = new RelayCommand(OpenPrintSetup, () => ActiveDocument is not null);
        CalibrationCommand = new RelayCommand(OpenCalibration, () => printerCatalog is not null && printerService is not null && calibrationRepository is not null);
        RecoveryCommand = new RelayCommand(OpenRecovery, () => recoveryCoordinator is not null);
        SettingsCommand = new AsyncRelayCommand(OpenSettingsAsync, () => Settings is not null && Catalog is not null);
        HelpCommand = new RelayCommand(OpenHelp, () => helpService?.GetTopics().Count > 0);
        CreateDocument();
    }

    public ObservableCollection<DocumentTabViewModel> Documents { get; } = [];
    public IRelayCommand NewDocumentCommand { get; }
    public IRelayCommand CloseActiveDocumentCommand { get; }
    public IRelayCommand ImportDataCommand { get; }
    public IRelayCommand PrintSetupCommand { get; }
    public IRelayCommand CalibrationCommand { get; }
    public IRelayCommand RecoveryCommand { get; }
    public IAsyncRelayCommand SettingsCommand { get; }
    public IRelayCommand HelpCommand { get; }
    public SettingsViewModel? Settings { get; }
    public TemplateCatalogViewModel? Catalog { get; }

    public event EventHandler<DataImportViewModel>? DataImportRequested;
    public event EventHandler<PrintSetupViewModel>? PrintSetupRequested;
    public event EventHandler<CalibrationWizardViewModel>? CalibrationRequested;
    public event EventHandler<RecoveryDialogViewModel>? RecoveryRequested;
    public event EventHandler? SettingsRequested;

    public DocumentTabViewModel? ActiveDocument
    {
        get => activeDocument;
        set
        {
            if (SetProperty(ref activeDocument, value))
            {
                CloseActiveDocumentCommand.NotifyCanExecuteChanged();
                ImportDataCommand.NotifyCanExecuteChanged();
                PrintSetupCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void CreateDocument()
    {
        DocumentTabViewModel document = new(TemplateDocument.CreateNew(DesktopText.Format("UntitledFormat", Documents.Count + 1)));
        AddDocument(document);
    }

    private void CloseActiveDocument()
    {
        if (ActiveDocument is null)
        {
            return;
        }

        int index = Documents.IndexOf(ActiveDocument);
        ActiveDocument.Session.Changed -= OnRecoveryRelevantChange;
        Documents.Remove(ActiveDocument);
        ActiveDocument.Dispose();
        ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
    }

    private void ImportData()
    {
        if (ActiveDocument is null) return;
        DataImportViewModel viewModel = new(dataReaders);
        viewModel.Imported += (_, snapshot) => ActiveDocument?.AttachDataSet(snapshot);
        DataImportRequested?.Invoke(this, viewModel);
    }

    private void OpenPrintSetup()
    {
        if (ActiveDocument is null) return;
        PrintSetupViewModel viewModel = new(printJobPlanner, printJobValidator, printPreviewService, printerCatalog, printerService, renderExporter);
        viewModel.Load(
            ActiveDocument.Session.State.Document,
            ActiveDocument.DataSet,
            ActiveDocument.Session.State.AssetContents);
        PrintSetupRequested?.Invoke(this, viewModel);
    }

    private void OpenCalibration()
    {
        if (printerCatalog is null || printerService is null || calibrationRepository is null) return;
        CalibrationRequested?.Invoke(this, new CalibrationWizardViewModel(printerCatalog, printerService, calibrationRepository));
    }

    private void OpenRecovery()
    {
        if (recoveryCoordinator is null) return;
        RecoveryDialogViewModel viewModel = new(recoveryCoordinator);
        viewModel.Restored += (_, content) => OpenRecovered(content);
        RecoveryRequested?.Invoke(this, viewModel);
    }

    private void OpenRecovered(Rmpp.Infrastructure.Templates.TemplatePackageContent content)
    {
        AddDocument(new DocumentTabViewModel(content.Document, content.Assets));
    }

    private async Task OpenSettingsAsync()
    {
        if (Settings is null || Catalog is null) return;
        await Settings.LoadAsync().ConfigureAwait(true);
        await Catalog.RefreshAsync().ConfigureAwait(true);
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenHelp() => helpService?.OpenTopicWithSystemHandler("getting-started");

    private void AddDocument(DocumentTabViewModel tab)
    {
        Documents.Add(tab);
        tab.Session.Changed += OnRecoveryRelevantChange;
        ActiveDocument = tab;
    }

    private void OnRecoveryRelevantChange(object? sender, Rmpp.Application.Documents.DocumentSessionChangedEventArgs e)
    {
        if (!e.State.IsDirty || recoveryCoordinator is null || sender is not Rmpp.Application.Documents.DocumentSession session) return;
        DocumentTabViewModel? tab = Documents.FirstOrDefault(item => ReferenceEquals(item.Session, session));
        if (tab is not null) recoveryCoordinator.QueueAutosave(tab.CreateRecoveryContent(), tab.OriginalTemplatePath);
    }
}
