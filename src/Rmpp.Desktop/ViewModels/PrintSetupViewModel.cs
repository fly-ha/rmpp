using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Application.Validation;
using Rmpp.Domain.Documents;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Desktop.ViewModels;

/// <summary>冻结一次打印设置并生成唯一的不可变计划，预览、PDF 和打印均消费该实例。</summary>
public sealed partial class PrintSetupViewModel : ObservableObject
{
    private readonly PrintJobPlanner planner;
    private readonly PrintJobValidator validator;
    private readonly WindowsPrinterCatalog? printerCatalog;
    private readonly PrintPreviewService previewService;
    private readonly IPrinterService? printerService;
    private readonly IRenderExporter? renderExporter;
    private TemplateDocument? document;
    private DataSetSnapshot? dataSet;

    public PrintSetupViewModel(
        PrintJobPlanner? planner = null,
        PrintJobValidator? validator = null,
        PrintPreviewService? previewService = null,
        WindowsPrinterCatalog? printerCatalog = null,
        IPrinterService? printerService = null,
        IRenderExporter? renderExporter = null)
    {
        this.planner = planner ?? new PrintJobPlanner();
        this.validator = validator ?? new PrintJobValidator();
        this.previewService = previewService ?? new PrintPreviewService();
        this.printerCatalog = printerCatalog;
        this.printerService = printerService;
        this.renderExporter = renderExporter;
        BuildPlanCommand = new RelayCommand(BuildPlan, () => document is not null);
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => Plan is not null && !HasBlockingErrors && SelectedPrinter is not null && SelectedMedia is not null);
        RefreshPrinters();
    }

    public ObservableCollection<WindowsPrinterCapabilities> Printers { get; } = [];
    public ObservableCollection<WindowsMediaDefinition> Media { get; } = [];
    public ObservableCollection<WindowsPrintResolution> Resolutions { get; } = [];
    public ObservableCollection<ValidationIssue> ValidationIssues { get; } = [];
    public IRelayCommand BuildPlanCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }

    [ObservableProperty]
    private WindowsPrinterCapabilities? selectedPrinter;

    [ObservableProperty]
    private WindowsMediaDefinition? selectedMedia;

    [ObservableProperty]
    private WindowsPrintResolution? selectedResolution;

    [ObservableProperty]
    private int firstRecord = 1;

    [ObservableProperty]
    private int lastRecord = 1;

    [ObservableProperty]
    private int recordCopies = 1;

    [ObservableProperty]
    private int jobCopies = 1;

    [ObservableProperty]
    private int? startingCell;

    [ObservableProperty]
    private PrintJobPlan? plan;

    [ObservableProperty]
    private string statusText = string.Empty;

    public bool HasBlockingErrors => ValidationIssues.Any(static issue => issue.BlocksOutput);
    public PrintPreviewViewModel? Preview { get; private set; }
    public PdfExportViewModel? PdfExport { get; private set; }

    public void Load(TemplateDocument template, DataSetSnapshot? sessionData)
    {
        document = template ?? throw new ArgumentNullException(nameof(template));
        dataSet = sessionData;
        FirstRecord = 1;
        LastRecord = Math.Max(1, sessionData?.Count ?? 1);
        Plan = null;
        BuildPlanCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPrinterChanged(WindowsPrinterCapabilities? value)
    {
        Media.Clear();
        Resolutions.Clear();
        if (value is not null)
        {
            foreach (WindowsMediaDefinition media in value.Media) Media.Add(media);
            foreach (WindowsPrintResolution resolution in value.Resolutions) Resolutions.Add(resolution);
        }
        SelectedMedia = Media.FirstOrDefault();
        SelectedResolution = Resolutions.FirstOrDefault();
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMediaChanged(WindowsMediaDefinition? value) => PrintCommand.NotifyCanExecuteChanged();

    private void RefreshPrinters()
    {
        Printers.Clear();
        if (printerCatalog is null) return;
        try
        {
            foreach (WindowsPrinterCapabilities printer in printerCatalog.GetPrinters()) Printers.Add(printer);
            SelectedPrinter = Printers.FirstOrDefault();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Printing.PrintSystemException)
        {
            StatusText = exception.Message;
        }
    }

    private void BuildPlan()
    {
        if (document is null) return;
        try
        {
            int available = dataSet?.Count ?? 1;
            PrintRecordSelection selection = new()
            {
                StartIndex = Math.Clamp(FirstRecord - 1, 0, Math.Max(0, available - 1)),
                EndIndexInclusive = Math.Clamp(LastRecord - 1, 0, Math.Max(0, available - 1)),
            };
            PrintJobRequest request = new()
            {
                Document = document,
                DataSet = dataSet,
                RecordSelection = selection,
                CopyPolicy = new PrintCopyPolicy { RecordCopies = RecordCopies, JobCopies = JobCopies },
                StartingCellOverride = StartingCell,
            };
            PrintJobPlan built = planner.Plan(request);
            PrintJobValidationResult validation = validator.Validate(request, built, printableArea: SelectedMedia?.PrintableArea);
            ValidationIssues.Clear();
            foreach (ValidationIssue issue in validation.Issues) ValidationIssues.Add(issue);
            Plan = built;
            Preview = new PrintPreviewViewModel(previewService);
            Preview.Load(built);
            PdfExport?.Dispose();
            PdfExport = renderExporter is null ? null : new PdfExportViewModel(renderExporter, previewService);
            PdfExport?.Load(built);
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(PdfExport));
            OnPropertyChanged(nameof(HasBlockingErrors));
            StatusText = $"{built.Pages.Count} 页，{built.TotalPlacements} 个版位，{ValidationIssues.Count} 个问题";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            Plan = null;
            Preview = null;
            PdfExport?.Dispose();
            PdfExport = null;
            ValidationIssues.Clear();
            StatusText = exception.Message;
        }
        PrintCommand.NotifyCanExecuteChanged();
    }

    private async Task PrintAsync()
    {
        if (Plan is null || printerService is null || SelectedPrinter is null || SelectedMedia is null) return;
        WindowsPrintResolution resolution = SelectedResolution ?? new WindowsPrintResolution(300, 300);
        PrintSubmissionRequest request = new()
        {
            PrinterId = SelectedPrinter.Identity.StableId,
            MediaName = SelectedMedia.Key,
            ResolutionDpi = Math.Max(resolution.DpiX, resolution.DpiY),
            Scenes = GetScenesAsync(Plan, RenderTarget.Print),
            JobName = Plan.DocumentSnapshot.Metadata.Title,
        };
        PrintSubmissionResult result = await printerService.SubmitAsync(request).ConfigureAwait(true);
        StatusText = result.WasCancelled ? $"已取消，已提交 {result.SubmittedPages} 页" : $"已提交 {result.SubmittedPages} 页";
    }

    private async IAsyncEnumerable<RenderScene> GetScenesAsync(PrintJobPlan jobPlan, RenderTarget target)
    {
        for (int page = 0; page < jobPlan.Pages.Count; page++)
        {
            yield return await previewService.GetPageAsync(jobPlan, page, target).ConfigureAwait(false);
        }
    }
}
