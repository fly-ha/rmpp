using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Domain.Printing;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Printing.Windows.Calibration;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Rendering.Scene;

namespace Rmpp.Desktop.ViewModels;

/// <summary>打印可测量校准页、计算修正并按稳定打印机/介质键保存本机档案。</summary>
public sealed partial class CalibrationWizardViewModel : ObservableObject
{
    private readonly WindowsPrinterCatalog catalog;
    private readonly IPrinterService printerService;
    private readonly CalibrationProfileRepository repository;

    public CalibrationWizardViewModel(WindowsPrinterCatalog catalog, IPrinterService printerService, CalibrationProfileRepository repository)
    {
        this.catalog = catalog;
        this.printerService = printerService;
        this.repository = repository;
        PrintTestPageCommand = new AsyncRelayCommand(PrintTestPageAsync, CanOperate);
        CalculateCommand = new RelayCommand(Calculate, CanOperate);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => PreviewProfile is not null);
        Refresh();
    }

    public ObservableCollection<WindowsPrinterCapabilities> Printers { get; } = [];
    public ObservableCollection<WindowsMediaDefinition> Media { get; } = [];
    public IAsyncRelayCommand PrintTestPageCommand { get; }
    public IRelayCommand CalculateCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    [ObservableProperty] private WindowsPrinterCapabilities? selectedPrinter;
    [ObservableProperty] private WindowsMediaDefinition? selectedMedia;
    [ObservableProperty] private double targetHorizontalMm = 100;
    [ObservableProperty] private double measuredHorizontalMm = 100;
    [ObservableProperty] private double targetVerticalMm = 100;
    [ObservableProperty] private double measuredVerticalMm = 100;
    [ObservableProperty] private double offsetXmm;
    [ObservableProperty] private double offsetYmm;
    [ObservableProperty] private double measuredRotationDegrees;
    [ObservableProperty] private CalibrationProfile? previewProfile;
    [ObservableProperty] private string statusText = string.Empty;

    partial void OnSelectedPrinterChanged(WindowsPrinterCapabilities? value)
    {
        Media.Clear();
        if (value is not null) foreach (WindowsMediaDefinition media in value.Media) Media.Add(media);
        SelectedMedia = Media.FirstOrDefault();
        NotifyCommands();
    }

    partial void OnSelectedMediaChanged(WindowsMediaDefinition? value) => NotifyCommands();

    private bool CanOperate() => SelectedPrinter is not null && SelectedMedia is not null;

    private void Refresh()
    {
        try
        {
            foreach (WindowsPrinterCapabilities printer in catalog.GetPrinters()) Printers.Add(printer);
            SelectedPrinter = Printers.FirstOrDefault();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Printing.PrintSystemException)
        {
            StatusText = exception.Message;
        }
    }

    private async Task PrintTestPageAsync()
    {
        if (SelectedPrinter is null || SelectedMedia is null) return;
        RenderScene scene = CalibrationPageFactory.Create(SelectedMedia.Size);
        PrintSubmissionResult result = await printerService.SubmitAsync(new PrintSubmissionRequest
        {
            PrinterId = SelectedPrinter.Identity.StableId,
            MediaName = SelectedMedia.Key,
            ResolutionDpi = 300,
            JobName = "RMPP 校准页",
            Scenes = SingleScene(scene),
        }).ConfigureAwait(true);
        StatusText = result.WasCancelled ? "校准页打印已取消。" : "校准页已提交，请测量纸面结果。";
    }

    private void Calculate()
    {
        if (SelectedPrinter is null || SelectedMedia is null) return;
        try
        {
            PreviewProfile = CalibrationCalculator.Calculate(
                new PrinterMediaKey(SelectedPrinter.Identity.StableId, SelectedMedia.Key),
                new CalibrationMeasurements
                {
                    TargetHorizontalMm = TargetHorizontalMm,
                    MeasuredHorizontalMm = MeasuredHorizontalMm,
                    TargetVerticalMm = TargetVerticalMm,
                    MeasuredVerticalMm = MeasuredVerticalMm,
                    OffsetXmm = OffsetXmm,
                    OffsetYmm = OffsetYmm,
                    MeasuredRotationDegrees = MeasuredRotationDegrees,
                }, DateTimeOffset.UtcNow);
            StatusText = $"预览：X {PreviewProfile.ScaleX:P4}，Y {PreviewProfile.ScaleY:P4}，旋转 {PreviewProfile.RotationDegrees:0.###}°";
            SaveCommand.NotifyCanExecuteChanged();
        }
        catch (ArgumentException exception) { StatusText = exception.Message; PreviewProfile = null; }
    }

    private async Task SaveAsync()
    {
        if (PreviewProfile is null) return;
        await repository.SaveAsync(PreviewProfile).ConfigureAwait(true);
        StatusText = $"校准档案已保存，验证时间：{PreviewProfile.LastVerifiedAt:yyyy-MM-dd HH:mm}";
    }

    private void NotifyCommands()
    {
        PrintTestPageCommand.NotifyCanExecuteChanged();
        CalculateCommand.NotifyCanExecuteChanged();
    }

    private static async IAsyncEnumerable<RenderScene> SingleScene(RenderScene scene)
    {
        yield return scene;
        await Task.CompletedTask;
    }
}
