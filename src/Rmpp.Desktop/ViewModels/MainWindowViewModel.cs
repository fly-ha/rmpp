using System.IO;
using System.Data.Common;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.Services;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;
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
    private readonly RmppPackageReader packageReader;
    private readonly AtomicTemplateFileWriter templateFileWriter;
    private readonly TemplateCatalogRepository? templateCatalogRepository;
    private readonly RecentFileRepository? recentFileRepository;

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
        LocalHelpService? helpService = null,
        RmppPackageReader? packageReader = null,
        AtomicTemplateFileWriter? templateFileWriter = null,
        TemplateCatalogRepository? templateCatalogRepository = null,
        RecentFileRepository? recentFileRepository = null)
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
        this.packageReader = packageReader ?? new RmppPackageReader();
        this.templateFileWriter = templateFileWriter ?? new AtomicTemplateFileWriter();
        this.templateCatalogRepository = templateCatalogRepository;
        this.recentFileRepository = recentFileRepository;
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

    /// <summary>从本地 `.rmpp` 包打开独立文档会话；相同路径已打开时只激活现有标签。</summary>
    public async Task OpenTemplateAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        DocumentTabViewModel? existing = Documents.FirstOrDefault(tab =>
            string.Equals(tab.OriginalTemplatePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveDocument = existing;
            return;
        }

        await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Rmpp.Infrastructure.Templates.TemplatePackageContent content = await packageReader.ReadAsync(stream, cancellationToken).ConfigureAwait(true);
        DocumentTabViewModel tab = new(content.Document, content.Assets, fullPath);
        AddDocument(tab);
        tab.Status.Text = DesktopText.Format("TemplateOpenedFormat", Path.GetFileName(fullPath));
        if (!await TryRecordTemplateAsync(fullPath, content.Document, content.PreviewPng, cancellationToken).ConfigureAwait(true))
        {
            tab.Status.Text += DesktopText.Get("CatalogSyncWarning");
        }
    }

    /// <summary>将活动会话原子保存到当前路径或指定的新路径，并同步本地目录元数据。</summary>
    public async Task<string> SaveActiveTemplateAsync(
        string? destinationPath = null,
        CancellationToken cancellationToken = default)
    {
        DocumentTabViewModel tab = ActiveDocument
            ?? throw new InvalidOperationException(DesktopText.Get("NoActiveTemplate"));
        string path = destinationPath ?? tab.OriginalTemplatePath
            ?? throw new InvalidOperationException(DesktopText.Get("TemplatePathRequired"));
        string fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".rmpp", StringComparison.OrdinalIgnoreCase))
        {
            fullPath += ".rmpp";
        }

        Rmpp.Infrastructure.Templates.TemplatePackageContent content = tab.CreateRecoveryContent();
        await templateFileWriter.WriteAsync(fullPath, content, cancellationToken).ConfigureAwait(true);
        tab.Session.MarkSaved(fullPath);
        tab.Status.Text = DesktopText.Format("TemplateSavedFormat", Path.GetFileName(fullPath));
        if (!await TryRecordTemplateAsync(fullPath, content.Document, content.PreviewPng, cancellationToken).ConfigureAwait(true))
        {
            tab.Status.Text += DesktopText.Get("CatalogSyncWarning");
        }
        return fullPath;
    }

    /// <summary>删除用户已确认的本地模板文件及可重建元数据，并关闭指向该文件的标签。</summary>
    public async Task DeleteTemplateAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        try
        {
            if (templateCatalogRepository is not null)
            {
                await templateCatalogRepository.RemoveAsync(fullPath, cancellationToken).ConfigureAwait(true);
            }
            if (recentFileRepository is not null)
            {
                await recentFileRepository.RemoveAsync(RecentFileKind.Template, fullPath, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (Exception exception) when (exception is DbException or IOException or InvalidOperationException)
        {
            // 模板文件删除不依赖可重建的 SQLite 元数据；后续目录扫描会再次校正状态。
        }

        foreach (DocumentTabViewModel tab in Documents
                     .Where(item => string.Equals(item.OriginalTemplatePath, fullPath, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            tab.Session.Changed -= OnRecoveryRelevantChange;
            Documents.Remove(tab);
            tab.Dispose();
        }

        ActiveDocument = Documents.LastOrDefault();
        if (Documents.Count == 0)
        {
            CreateDocument();
        }
        try
        {
            if (Catalog is not null)
            {
                await Catalog.RefreshAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception) when (exception is DbException or IOException or InvalidOperationException)
        {
            // 文件生命周期已完成，目录刷新失败不回滚文件结果。
        }
    }

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
        DocumentTabViewModel tab = ActiveDocument;
        PrintSetupViewModel viewModel = new(
            printJobPlanner,
            printJobValidator,
            printPreviewService,
            printerCatalog,
            printerService,
            renderExporter,
            settings => tab.Dispatcher.Execute(new ChangePrintSettingsCommand(settings)));
        viewModel.Load(
            tab.Session.State.Document,
            tab.DataSet,
            tab.Session.State.AssetContents);
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

    private async Task RecordTemplateAsync(
        string path,
        TemplateDocument document,
        byte[]? previewPng,
        CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        if (templateCatalogRepository is not null)
        {
            await templateCatalogRepository.UpsertAsync(new TemplateCatalogEntry
            {
                DocumentId = document.Id,
                Path = path,
                Title = document.Metadata.Title,
                Description = document.Metadata.Description,
                FileModifiedAt = file.LastWriteTimeUtc,
                DocumentModifiedAt = document.Metadata.ModifiedAt,
                LastScannedAt = DateTimeOffset.UtcNow,
                Status = TemplateCatalogStatus.Available,
                ThumbnailPng = previewPng,
            }, cancellationToken).ConfigureAwait(true);
        }
        if (recentFileRepository is not null)
        {
            await recentFileRepository.AddAsync(new RecentFileEntry
            {
                Kind = RecentFileKind.Template,
                Path = path,
                DisplayName = document.Metadata.Title,
                LastOpenedAt = DateTimeOffset.UtcNow,
            }, Settings?.RecentMaximumEntries ?? 20, cancellationToken).ConfigureAwait(true);
        }
        if (Catalog is not null)
        {
            await Catalog.RefreshAsync().ConfigureAwait(true);
        }
    }

    private async Task<bool> TryRecordTemplateAsync(
        string path,
        TemplateDocument document,
        byte[]? previewPng,
        CancellationToken cancellationToken)
    {
        try
        {
            await RecordTemplateAsync(path, document, previewPng, cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (Exception exception) when (exception is DbException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private void OnRecoveryRelevantChange(object? sender, Rmpp.Application.Documents.DocumentSessionChangedEventArgs e)
    {
        if (!e.State.IsDirty || recoveryCoordinator is null || sender is not Rmpp.Application.Documents.DocumentSession session) return;
        DocumentTabViewModel? tab = Documents.FirstOrDefault(item => ReferenceEquals(item.Session, session));
        if (tab is not null) recoveryCoordinator.QueueAutosave(tab.CreateRecoveryContent(), tab.OriginalTemplatePath);
    }
}
