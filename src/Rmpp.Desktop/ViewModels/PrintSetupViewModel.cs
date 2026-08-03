using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Application.Validation;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Printing;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;

namespace Rmpp.Desktop.ViewModels;

/// <summary>异步准备本机打印能力，并把模板默认值、不可变计划、预览、PDF 和打印统一在一次设置会话中。</summary>
public sealed partial class PrintSetupViewModel : ObservableObject
{
    private const double MediaMatchToleranceMm = 0.5;
    private readonly PrintJobPlanner planner;
    private readonly PrintJobValidator validator;
    private readonly WindowsPrinterCatalog? printerCatalog;
    private readonly PrintPreviewService previewService;
    private readonly IPrinterService? printerService;
    private readonly IRenderExporter? renderExporter;
    private readonly Action<TemplatePrintSettings>? persistPrintSettings;
    private TemplateDocument? document;
    private DataSetSnapshot? dataSet;
    private IRenderAssetProvider? assetProvider;
    private string? preferredPrinterId;

    public PrintSetupViewModel(
        PrintJobPlanner? planner = null,
        PrintJobValidator? validator = null,
        PrintPreviewService? previewService = null,
        WindowsPrinterCatalog? printerCatalog = null,
        IPrinterService? printerService = null,
        IRenderExporter? renderExporter = null,
        Action<TemplatePrintSettings>? persistPrintSettings = null)
    {
        this.planner = planner ?? new PrintJobPlanner();
        this.validator = validator ?? new PrintJobValidator();
        this.previewService = previewService ?? new PrintPreviewService();
        this.printerCatalog = printerCatalog;
        this.printerService = printerService;
        this.renderExporter = renderExporter;
        this.persistPrintSettings = persistPrintSettings;
        BuildPlanCommand = new RelayCommand(BuildPlan, () => document is not null);
        PrintCommand = new AsyncRelayCommand(PrintAsync, CanPrint);
        RefreshPrintersCommand = new AsyncRelayCommand(RefreshPrintersAsync, () => printerCatalog is not null && !IsLoadingPrinters);
        SaveTemplatePrintSettingsCommand = new RelayCommand(SaveTemplatePrintSettings, () => document is not null && persistPrintSettings is not null);
    }

    public ObservableCollection<WindowsPrinterCapabilities> Printers { get; } = [];
    public ObservableCollection<WindowsMediaDefinition> Media { get; } = [];
    public ObservableCollection<WindowsPrintResolution> Resolutions { get; } = [];
    public ObservableCollection<ValidationIssue> ValidationIssues { get; } = [];
    public IReadOnlyList<PrintCopyOrder> CopyOrders { get; } = Enum.GetValues<PrintCopyOrder>();
    public IReadOnlyList<PrintMediaOrientation> Orientations { get; } = Enum.GetValues<PrintMediaOrientation>();
    public IRelayCommand BuildPlanCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }
    public IAsyncRelayCommand RefreshPrintersCommand { get; }
    public IRelayCommand SaveTemplatePrintSettingsCommand { get; }

    [ObservableProperty] private WindowsPrinterCapabilities? selectedPrinter;
    [ObservableProperty] private WindowsMediaDefinition? selectedMedia;
    [ObservableProperty] private WindowsPrintResolution? selectedResolution;
    [ObservableProperty] private PrintMediaOrientation selectedOrientation = PrintMediaOrientation.Portrait;
    [ObservableProperty] private int firstRecord = 1;
    [ObservableProperty] private int lastRecord = 1;
    [ObservableProperty] private int copies = 1;
    [ObservableProperty] private PrintCopyOrder copyOrder = PrintCopyOrder.PerRecord;
    [ObservableProperty] private int outputCount = 1;
    [ObservableProperty] private int? startingCell;
    [ObservableProperty] private double customWidthMm = 210;
    [ObservableProperty] private double customHeightMm = 297;
    [ObservableProperty] private bool isLoadingPrinters;
    [ObservableProperty] private PrintJobPlan? plan;
    [ObservableProperty] private string statusText = string.Empty;

    public bool HasBlockingErrors => ValidationIssues.Any(static issue => issue.BlocksOutput);
    public bool HasDataSource => dataSet is { Count: > 0 };
    public int MaximumRecord => Math.Max(1, dataSet?.Count ?? 1);
    public bool HasSheetLabelLayout => document?.Page.Layout is SheetLabelLayout;
    public int MaximumStartingCell => document?.Page.Layout is SheetLabelLayout sheet
        ? checked(sheet.Rows * sheet.Columns)
        : 1;
    public string DataSourceModeText => HasDataSource
        ? $"已导入 {dataSet!.Count} 条记录；默认全部，可在 1 至 {dataSet.Count} 内调整。"
        : "当前没有数据源；输出数量决定生成的逻辑记录数和流水号页数。";
    public string StartingCellExplanation => HasSheetLabelLayout
        ? $"标签纸起始版位按当前遍历顺序编号，范围 1 至 {MaximumStartingCell}；1 表示第一个物理标签格。"
        : string.Empty;
    public PrintPreviewViewModel? Preview { get; private set; }
    public PdfExportViewModel? PdfExport { get; private set; }
    /// <summary>为缓冲数字控件提供统一 double 入口，提交前不会触发打印计划重建。</summary>
    public double FirstRecordValue { get => FirstRecord; set => FirstRecord = ToPositiveInt(value, FirstRecord); }
    public double LastRecordValue { get => LastRecord; set => LastRecord = ToPositiveInt(value, LastRecord); }
    public double OutputCountValue { get => OutputCount; set => OutputCount = ToPositiveInt(value, OutputCount); }
    public double CopiesValue { get => Copies; set => Copies = ToPositiveInt(value, Copies); }
    public double? StartingCellValue
    {
        get => StartingCell;
        set => StartingCell = value is null ? null : ToPositiveInt(value.Value, StartingCell ?? 1);
    }
    public string CopyOrderExplanation => CopyOrder == PrintCopyOrder.PerRecord
        ? "逐记录：1、1、2、2、3、3"
        : "整批分页：1、2、3、1、2、3";
    public string SerialSummary
    {
        get
        {
            SerialElement? serial = document?.Elements.OfType<SerialElement>().FirstOrDefault();
            if (serial is null) return "模板中没有流水号元素。";
            int logicalCount = dataSet is null
                ? Math.Max(1, OutputCount)
                : Math.Max(1, Math.Abs(LastRecord - FirstRecord) + 1);
            long last = checked(serial.Definition.Start + serial.Definition.Step * (logicalCount - 1L));
            return $"流水号：{serial.Definition.Start} → {last}，共 {logicalCount} 个基础记录；副本按所选顺序重复。";
        }
    }
    public string MediaMismatchText
    {
        get
        {
            if (document is null || SelectedMedia is null) return string.Empty;
            MmSize template = OrientedTemplateSize();
            double widthDifference = SelectedMedia.Size.Width - template.Width;
            double heightDifference = SelectedMedia.Size.Height - template.Height;
            if (Math.Abs(widthDifference) <= MediaMatchToleranceMm && Math.Abs(heightDifference) <= MediaMatchToleranceMm)
            {
                return "介质尺寸与模板一致，按 100% 物理尺寸输出。";
            }
            return $"介质与模板不一致：宽差 {widthDifference:+0.###;-0.###;0} mm，高差 {heightDifference:+0.###;-0.###;0} mm；不会自动缩放。";
        }
    }

    public void Load(
        TemplateDocument template,
        DataSetSnapshot? sessionData,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? assetContents = null)
    {
        document = template ?? throw new ArgumentNullException(nameof(template));
        dataSet = sessionData;
        assetProvider = assetContents is { Count: > 0 }
            ? new PackageRenderAssetProvider(template, assetContents)
            : null;
        TemplatePrintSettings defaults = template.PrintSettings;
        preferredPrinterId = defaults.PreferredPrinterId;
        FirstRecord = defaults.UseAllRecords ? 1 : Math.Clamp(defaults.FirstRecord, 1, MaximumRecord);
        LastRecord = defaults.UseAllRecords ? MaximumRecord : Math.Clamp(defaults.LastRecord, FirstRecord, MaximumRecord);
        OutputCount = defaults.OutputCount;
        Copies = defaults.Copies;
        CopyOrder = defaults.CopyOrder;
        StartingCell = document.Page.Layout is SheetLabelLayout sheet
            ? Math.Clamp(defaults.StartingCell ?? sheet.StartingCell, 1, checked(sheet.Rows * sheet.Columns))
            : null;
        SelectedOrientation = template.Page.Media.Orientation == PageOrientation.Landscape
            ? PrintMediaOrientation.Landscape
            : PrintMediaOrientation.Portrait;
        MmSize size = OrientedTemplateSize();
        CustomWidthMm = size.Width;
        CustomHeightMm = size.Height;
        Plan = null;
        PopulateMedia();
        OnPropertyChanged(nameof(HasDataSource));
        OnPropertyChanged(nameof(MaximumRecord));
        OnPropertyChanged(nameof(HasSheetLabelLayout));
        OnPropertyChanged(nameof(MaximumStartingCell));
        OnPropertyChanged(nameof(DataSourceModeText));
        OnPropertyChanged(nameof(StartingCellExplanation));
        OnPropertyChanged(nameof(SerialSummary));
        BuildPlanCommand.NotifyCanExecuteChanged();
        SaveTemplatePrintSettingsCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPrinterChanged(WindowsPrinterCapabilities? value)
    {
        PopulateMedia();
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMediaChanged(WindowsMediaDefinition? value)
    {
        OnPropertyChanged(nameof(MediaMismatchText));
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedOrientationChanged(PrintMediaOrientation value) => OnPropertyChanged(nameof(MediaMismatchText));
    partial void OnCopyOrderChanged(PrintCopyOrder value) { OnPropertyChanged(nameof(CopyOrderExplanation)); OnPropertyChanged(nameof(SerialSummary)); }
    partial void OnCopiesChanged(int value) { OnPropertyChanged(nameof(CopyOrderExplanation)); OnPropertyChanged(nameof(CopiesValue)); }
    partial void OnOutputCountChanged(int value) { OnPropertyChanged(nameof(SerialSummary)); OnPropertyChanged(nameof(OutputCountValue)); }
    partial void OnFirstRecordChanged(int value)
    {
        if (HasDataSource)
        {
            int clamped = Math.Clamp(value, 1, MaximumRecord);
            if (clamped != value) { FirstRecord = clamped; return; }
            if (LastRecord < clamped) LastRecord = clamped;
        }
        OnPropertyChanged(nameof(SerialSummary));
        OnPropertyChanged(nameof(FirstRecordValue));
    }
    partial void OnLastRecordChanged(int value)
    {
        if (HasDataSource)
        {
            int clamped = Math.Clamp(value, FirstRecord, MaximumRecord);
            if (clamped != value) { LastRecord = clamped; return; }
        }
        OnPropertyChanged(nameof(SerialSummary));
        OnPropertyChanged(nameof(LastRecordValue));
    }
    partial void OnStartingCellChanged(int? value) => OnPropertyChanged(nameof(StartingCellValue));
    partial void OnCustomWidthMmChanged(double value) => RefreshCustomMedia();
    partial void OnCustomHeightMmChanged(double value) => RefreshCustomMedia();
    partial void OnIsLoadingPrintersChanged(bool value) => RefreshPrintersCommand.NotifyCanExecuteChanged();

    private async Task RefreshPrintersAsync()
    {
        if (printerCatalog is null) return;
        IsLoadingPrinters = true;
        StatusText = "正在读取本机打印机能力…";
        try
        {
            IReadOnlyList<WindowsPrinterCapabilities> loaded = await printerCatalog.GetPrintersAsync().ConfigureAwait(true);
            Printers.Clear();
            foreach (WindowsPrinterCapabilities printer in loaded) Printers.Add(printer);
            WindowsPrinterCapabilities? preferred = string.IsNullOrWhiteSpace(preferredPrinterId)
                ? null
                : Printers.FirstOrDefault(printer => string.Equals(printer.Identity.StableId, preferredPrinterId, StringComparison.Ordinal));
            SelectedPrinter = preferred
                ?? Printers.FirstOrDefault(static printer => printer.IsDefault)
                ?? Printers.FirstOrDefault();
            StatusText = Printers.Count == 0
                ? "没有发现已安装打印机，可先导出 PDF。"
                : preferred is not null
                    ? $"已恢复模板首选打印机：{preferred.Identity.DisplayName}。"
                    : !string.IsNullOrWhiteSpace(preferredPrinterId)
                        ? $"模板首选打印机未安装，已回退到 {SelectedPrinter!.Identity.DisplayName}。"
                        : $"已加载 {Printers.Count} 台打印机。";
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Printing.PrintSystemException)
        {
            StatusText = $"打印机能力读取失败：{exception.Message}；仍可导出 PDF。";
        }
        finally
        {
            IsLoadingPrinters = false;
        }
    }

    private void PopulateMedia()
    {
        WindowsMediaDefinition? previous = SelectedMedia;
        Media.Clear();
        if (SelectedPrinter is not null)
        {
            foreach (WindowsMediaDefinition media in SelectedPrinter.Media) Media.Add(media);
            Resolutions.Clear();
            foreach (WindowsPrintResolution resolution in SelectedPrinter.Resolutions) Resolutions.Add(resolution);
            SelectedResolution = Resolutions.FirstOrDefault();
        }
        else
        {
            Resolutions.Clear();
            SelectedResolution = null;
        }

        WindowsMediaDefinition custom = CreateCustomMedia();
        Media.Add(custom);
        MmSize template = OrientedTemplateSize();
        SelectedMedia = Media.FirstOrDefault(media =>
            !media.IsCustom
            && Math.Abs(media.Size.Width - template.Width) <= MediaMatchToleranceMm
            && Math.Abs(media.Size.Height - template.Height) <= MediaMatchToleranceMm)
            ?? (previous is not null ? Media.FirstOrDefault(media => media.Key == previous.Key) : null)
            ?? custom;
    }

    private void RefreshCustomMedia()
    {
        if (document is null || CustomWidthMm <= 0 || CustomHeightMm <= 0 || Media.Count == 0) return;
        bool customWasSelected = SelectedMedia?.IsCustom == true;
        WindowsMediaDefinition? old = Media.FirstOrDefault(static media => media.IsCustom);
        if (old is not null) Media.Remove(old);
        WindowsMediaDefinition replacement = CreateCustomMedia();
        Media.Add(replacement);
        if (customWasSelected) SelectedMedia = replacement;
        OnPropertyChanged(nameof(MediaMismatchText));
    }

    private WindowsMediaDefinition CreateCustomMedia()
    {
        MmSize size = new(Math.Max(0.1, CustomWidthMm), Math.Max(0.1, CustomHeightMm));
        return new WindowsMediaDefinition
        {
            Key = $"custom:{size.Width:0.###}x{size.Height:0.###}",
            DisplayName = $"自定义（模板） {size.Width:0.###} × {size.Height:0.###} mm",
            DriverName = "Custom",
            Size = size,
            PrintableArea = new MmRect(0, 0, size.Width, size.Height),
            IsCustom = true,
        };
    }

    private void BuildPlan()
    {
        if (document is null) return;
        try
        {
            int available = HasDataSource ? dataSet!.Count : 1;
            PrintRecordSelection selection = new()
            {
                StartIndex = Math.Clamp(FirstRecord - 1, 0, Math.Max(0, available - 1)),
                EndIndexInclusive = Math.Clamp(LastRecord - 1, 0, Math.Max(0, available - 1)),
            };
            PrintJobRequest request = new()
            {
                Document = document,
                DataSet = HasDataSource ? dataSet : null,
                RecordSelection = selection,
                CopyPolicy = new PrintCopyPolicy { Copies = Copies, Order = CopyOrder },
                OutputCount = HasDataSource ? 1 : OutputCount,
                StartingCellOverride = HasSheetLabelLayout ? StartingCell : null,
            };
            PrintJobPlan built = planner.Plan(request);
            PrintJobValidationResult validation = validator.Validate(request, built, printableArea: SelectedMedia?.PrintableArea);
            ValidationIssues.Clear();
            foreach (ValidationIssue issue in validation.Issues) ValidationIssues.Add(issue);
            Plan = built;
            Preview?.Dispose();
            Preview = new PrintPreviewViewModel(previewService, assetProvider: assetProvider);
            Preview.Load(built);
            PdfExport?.Dispose();
            PdfExport = renderExporter is null ? null : new PdfExportViewModel(renderExporter, previewService, assetProvider);
            PdfExport?.Load(built);
            if (PdfExport is not null) PdfExport.IncludePrintableBackgrounds = document.PrintSettings.IncludePrintableBackgrounds;
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(PdfExport));
            OnPropertyChanged(nameof(HasBlockingErrors));
            StatusText = $"计划：{built.Pages.Count} 页，{built.TotalPlacements} 个版位，{ValidationIssues.Count} 个问题。";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            Plan = null;
            Preview?.Dispose();
            Preview = null;
            PdfExport?.Dispose();
            PdfExport = null;
            ValidationIssues.Clear();
            StatusText = exception.Message;
        }
        PrintCommand.NotifyCanExecuteChanged();
    }

    private void SaveTemplatePrintSettings()
    {
        if (document is null || persistPrintSettings is null) return;
        TemplatePrintSettings settings = new()
        {
            PreferredPrinterId = SelectedPrinter?.Identity.StableId ?? document.PrintSettings.PreferredPrinterId,
            PreferredPrinterDisplayName = SelectedPrinter?.Identity.DisplayName ?? document.PrintSettings.PreferredPrinterDisplayName,
            OutputCount = OutputCount,
            UseAllRecords = HasDataSource && FirstRecord == 1 && LastRecord == dataSet!.Count,
            FirstRecord = FirstRecord,
            LastRecord = LastRecord,
            Copies = Copies,
            CopyOrder = CopyOrder,
            StartingCell = HasSheetLabelLayout ? StartingCell : null,
            IncludePrintableBackgrounds = PdfExport?.IncludePrintableBackgrounds ?? document.PrintSettings.IncludePrintableBackgrounds,
        };
        settings.Validate();
        persistPrintSettings(settings);
        document = document with { PrintSettings = settings };
        preferredPrinterId = settings.PreferredPrinterId;
        StatusText = settings.PreferredPrinterDisplayName is { Length: > 0 } printerName
            ? $"打印默认值与首选打印机“{printerName}”已保存到模板；分辨率和校准仍保存在本机。"
            : "当前打印默认值已保存到模板。";
    }

    private bool CanPrint() => Plan is not null
        && !HasBlockingErrors
        && SelectedPrinter is not null
        && SelectedMedia is not null
        && SelectedResolution is not null;

    private async Task PrintAsync()
    {
        if (Plan is null || printerService is null || SelectedPrinter is null || SelectedMedia is null || SelectedResolution is null) return;
        PrintSubmissionRequest request = new()
        {
            PrinterId = SelectedPrinter.Identity.StableId,
            MediaName = SelectedMedia.Key,
            CustomMediaSize = SelectedMedia.IsCustom ? SelectedMedia.Size : null,
            Orientation = SelectedOrientation,
            ResolutionDpi = Math.Max(SelectedResolution.DpiX, SelectedResolution.DpiY),
            Scenes = GetScenesAsync(Plan, RenderTarget.Print),
            AssetProvider = assetProvider,
            JobName = Plan.DocumentSnapshot.Metadata.Title,
        };
        PrintSubmissionResult result = await printerService.SubmitAsync(request).ConfigureAwait(true);
        StatusText = result.WasCancelled ? $"已取消，已提交 {result.SubmittedPages} 页。" : $"已提交 {result.SubmittedPages} 页。";
    }

    private async IAsyncEnumerable<RenderScene> GetScenesAsync(PrintJobPlan jobPlan, RenderTarget target)
    {
        for (int page = 0; page < jobPlan.Pages.Count; page++)
        {
            yield return await previewService.GetPageAsync(jobPlan, page, target).ConfigureAwait(false);
        }
    }

    private MmSize OrientedTemplateSize()
    {
        if (document is null) return new MmSize(Math.Max(0.1, CustomWidthMm), Math.Max(0.1, CustomHeightMm));
        MmSize size = document.Page.Media.Size;
        return document.Page.Media.Orientation == PageOrientation.Landscape
            ? new MmSize(size.Height, size.Width)
            : size;
    }

    private static int ToPositiveInt(double value, int fallback) =>
        double.IsFinite(value) && value >= 1 && value <= int.MaxValue ? checked((int)value) : fallback;
}
