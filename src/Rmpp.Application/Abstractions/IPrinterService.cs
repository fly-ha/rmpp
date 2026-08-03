using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Abstractions;

/// <summary>定义提交给打印基础设施的稳定打印机、介质和场景流。</summary>
public sealed record PrintSubmissionRequest
{
    public required string PrinterId { get; init; }
    public required string MediaName { get; init; }
    /// <summary>驱动未列出对应介质时使用的自定义物理尺寸；打印后端仍可拒绝不支持的尺寸。</summary>
    public MmSize? CustomMediaSize { get; init; }
    public required IAsyncEnumerable<RenderScene> Scenes { get; init; }
    /// <summary>解析模板包内图片资源，确保图片元素和二维码中心图标可进入本机打印后端。</summary>
    public IRenderAssetProvider? AssetProvider { get; init; }
    public int Copies { get; init; } = 1;
    public PrintMediaOrientation Orientation { get; init; } = PrintMediaOrientation.Portrait;
    public int ResolutionDpi { get; init; } = 300;
    public string JobName { get; init; } = "RMPP";
    public bool AllowFitToPageScaling { get; init; }
}

/// <summary>跨平台无关的打印介质方向，避免应用层引用 Windows PrintTicket 类型。</summary>
public enum PrintMediaOrientation
{
    Portrait,
    Landscape,
    ReversePortrait,
    ReverseLandscape,
}

public sealed record PrintSubmissionProgress(int SubmittedPages, int? TotalPages, string Message);

public sealed record PrintSubmissionResult(int SubmittedPages, bool WasCancelled, string? SpoolerJobId);

/// <summary>隔离 Windows 驱动发现和提交，使应用层可使用假实现测试失败与取消。</summary>
public interface IPrinterService
{
    Task<PrintSubmissionResult> SubmitAsync(
        PrintSubmissionRequest request,
        IProgress<PrintSubmissionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
