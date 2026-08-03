using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Application.Editing.Snapping;
using Rmpp.Desktop.Resources;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Desktop.ViewModels;

/// <summary>保存纯视图设置，并把所有编辑动作转为应用命令。</summary>
public sealed class DesignerViewModel : ObservableObject, IDisposable
{
    private readonly DocumentSession session;
    private readonly EditorCommandDispatcher dispatcher;
    private double zoom = 1;
    private bool showGrid = true;
    private bool snappingEnabled = true;
    private double gridSpacingMm = 5;
    private string snapIndicator = string.Empty;
    private DesignerTool activeTool;
    private string backgroundStatus = string.Empty;
    private readonly IReadOnlyList<DesignerToolItem> tools = ToolItems;

    private static readonly DesignerToolItem[] ToolItems =
    [
        new(DesignerTool.Select, "ToolSelect"),
        new(DesignerTool.Text, "ToolText"),
        new(DesignerTool.DateTime, "ToolDateTime"),
        new(DesignerTool.Serial, "ToolSerial"),
        new(DesignerTool.Image, "ToolImage"),
        new(DesignerTool.Barcode, "ToolBarcode"),
        new(DesignerTool.QrCode, "ToolQrCode"),
        new(DesignerTool.DataMatrix, "ToolDataMatrix"),
        new(DesignerTool.Line, "ToolLine"),
        new(DesignerTool.Rectangle, "ToolRectangle"),
        new(DesignerTool.RoundedRectangle, "ToolRoundedRectangle"),
        new(DesignerTool.Ellipse, "ToolEllipse"),
        new(DesignerTool.Arc, "ToolArc"),
        new(DesignerTool.Sector, "ToolSector"),
        new(DesignerTool.Polyline, "ToolPolyline"),
        new(DesignerTool.Polygon, "ToolPolygon"),
    ];

    public DesignerViewModel(DocumentSession session, EditorCommandDispatcher dispatcher)
    {
        this.session = session;
        this.dispatcher = dispatcher;
        session.Changed += OnSessionChanged;
    }

    public TemplateDocument Document => session.State.Document;
    public IReadOnlySet<Guid> SelectedElementIds => session.State.SelectedElementIds;
    public IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> AssetContents => session.State.AssetContents;
    public IReadOnlyList<DesignerToolItem> Tools => tools;
    public IReadOnlyList<ImageFitMode> BackgroundFitModes { get; } = Enum.GetValues<ImageFitMode>();

    public DesignerTool ActiveTool
    {
        get => activeTool;
        set => SetProperty(ref activeTool, value);
    }

    public double Zoom
    {
        get => zoom;
        set => SetProperty(ref zoom, Math.Clamp(value, 0.1, 8));
    }

    public bool ShowGrid { get => showGrid; set => SetProperty(ref showGrid, value); }
    public bool SnappingEnabled { get => snappingEnabled; set => SetProperty(ref snappingEnabled, value); }
    public double GridSpacingMm { get => gridSpacingMm; set => SetProperty(ref gridSpacingMm, Math.Clamp(value, 0.1, 100)); }
    public string SnapIndicator { get => snapIndicator; private set => SetProperty(ref snapIndicator, value); }
    public string BackgroundStatus { get => backgroundStatus; private set => SetProperty(ref backgroundStatus, value); }
    public bool HasBackground => ActiveBackground is not null;

    public bool ActiveBackgroundVisible
    {
        get => ActiveBackground?.IsVisible ?? false;
        set => ChangeActiveBackground(background => background with { IsVisible = value });
    }

    public bool ActiveBackgroundPrintable
    {
        get => ActiveBackground?.IsPrintable ?? false;
        set => ChangeActiveBackground(background => background with { IsPrintable = value });
    }

    public bool ActiveBackgroundLocked
    {
        get => ActiveBackground?.IsLocked ?? false;
        set => ChangeActiveBackground(background => background with { IsLocked = value });
    }

    public double ActiveBackgroundOpacity
    {
        get => ActiveBackground?.Opacity ?? 1;
        set => ChangeActiveBackground(background => background with { Opacity = Math.Clamp(value, 0, 1) }, "background-opacity");
    }

    public int ActiveBackgroundPdfPageNumber
    {
        get => ActiveBackground?.PdfPageNumber ?? 1;
        set => ChangeActiveBackground(background => background with { PdfPageNumber = Math.Max(1, value) });
    }

    public ImageFitMode ActiveBackgroundFitMode
    {
        get => ActiveBackground?.FitMode ?? ImageFitMode.Contain;
        set => ChangeActiveBackground(background => background with { FitMode = value });
    }

    public void Select(Guid? elementId, bool additive)
    {
        if (elementId is null)
        {
            session.SetSelection(Array.Empty<Guid>());
            return;
        }

        HashSet<Guid> selection = additive ? session.State.SelectedElementIds.ToHashSet() : [];
        if (!selection.Add(elementId.Value) && additive)
        {
            selection.Remove(elementId.Value);
        }

        session.SetSelection(selection);
    }

    public void MoveSelection(double deltaXmm, double deltaYmm, bool temporarilyDisableSnap = false)
    {
        if (session.State.SelectedElementIds.Count == 0)
        {
            return;
        }

        TemplateElement? anchor = session.State.Document.Elements.FirstOrDefault(
            element => session.State.SelectedElementIds.Contains(element.Id));
        MmPoint adjusted = new(deltaXmm, deltaYmm);
        if (anchor is not null)
        {
            SnapResult snap = SnapEngine.Snap(
                anchor.Bounds,
                adjusted,
                new SnapContext(
                    Document.Page.Media.Size,
                    Document.Guides,
                    Document.Elements,
                    session.State.SelectedElementIds),
                new SnapOptions
                {
                    IsEnabled = SnappingEnabled,
                    IsTemporarilyDisabled = temporarilyDisableSnap,
                    GridSpacingMm = GridSpacingMm,
                    PixelsPerMillimetre = 96d / 25.4 * Zoom,
                });
            adjusted = snap.AdjustedDelta;
            SnapIndicator = snap.Snapped
                ? string.Join(" / ", new[] { snap.HorizontalMatch?.Candidate.Label, snap.VerticalMatch?.Candidate.Label }.Where(static label => label is not null))
                : string.Empty;
        }

        _ = dispatcher.Execute(new MoveElementsCommand(
            session.State.SelectedElementIds,
            adjusted.X,
            adjusted.Y,
            "designer-move"));
    }

    public void ResizePrimary(MmRect bounds)
    {
        Guid? id = SelectedElementIds.FirstOrDefault();
        if (id is { } elementId && elementId != Guid.Empty)
        {
            _ = dispatcher.Execute(new ResizeElementsCommand(new Dictionary<Guid, MmRect> { [elementId] = bounds }, "designer-resize"));
        }
    }

    public void RotatePrimary(Angle angle)
    {
        Guid? id = SelectedElementIds.FirstOrDefault();
        if (id is { } elementId && elementId != Guid.Empty)
        {
            _ = dispatcher.Execute(new RotateElementsCommand(new Dictionary<Guid, Angle> { [elementId] = angle }, "designer-rotate"));
        }
    }

    public void EditPrimaryPoints(IReadOnlyList<MmPoint> points)
    {
        Guid? id = SelectedElementIds.FirstOrDefault();
        if (id is { } elementId && elementId != Guid.Empty)
        {
            _ = dispatcher.Execute(new EditPointsCommand(elementId, points, "designer-points"));
        }
    }

    public void DuplicateSelection()
    {
        TemplateElement[] copies = Document.Elements
            .Where(element => SelectedElementIds.Contains(element.Id))
            .Select(element => element with
            {
                Id = Guid.NewGuid(),
                Name = element.Name + DesktopText.Get("DuplicateSuffix"),
                Bounds = element.Bounds.Translate(5, 5),
            })
            .ToArray();
        if (copies.Length > 0 && dispatcher.Execute(new AddElementsCommand(copies)))
        {
            session.SetSelection(copies.Select(static element => element.Id));
        }
    }

    /// <summary>使用工具类型和画布毫米矩形创建合法的首版元素，并统一进入应用命令与撤销历史。</summary>
    public void CreateElement(DesignerTool tool, MmRect requestedBounds)
    {
        if (tool == DesignerTool.Select || Document.Layers.Count == 0)
        {
            return;
        }

        MmRect bounds = NormalizeCreationBounds(requestedBounds);
        Guid layerId = Document.Layers.FirstOrDefault(static layer => !layer.IsLocked)?.Id
            ?? Document.Layers[0].Id;
        int zIndex = Document.Elements.Count == 0 ? 0 : Document.Elements.Max(static element => element.ZIndex) + 1;
        TemplateElement element = CreateElementCore(tool, layerId, zIndex, bounds);
        if (dispatcher.Execute(new AddElementsCommand([element])))
        {
            session.SetSelection([element.Id]);
            ActiveTool = DesignerTool.Select;
        }
    }

    /// <summary>从用户选择的本地文件导入包内背景资源；默认锁定、显示且不参与打印，适合预印表单套打。</summary>
    public async Task ImportBackgroundAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            string fileName = Path.GetFileName(filePath);
            string mediaType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".pdf" => "application/pdf",
                _ => throw new InvalidDataException(DesktopText.Get("BackgroundUnsupportedType")),
            };
            byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(true);
            Guid assetId = Guid.NewGuid();
            AssetReference asset = new(assetId, fileName, mediaType, AssetHashService.ComputeSha256(bytes));
            _ = new AssetStore().Inspect(asset, bytes);
            BackgroundDefinition background = new()
            {
                AssetId = assetId,
                Bounds = new MmRect(0, 0, Document.Page.Media.Size.Width, Document.Page.Media.Size.Height),
                FitMode = ImageFitMode.Contain,
                Opacity = 1,
                IsVisible = true,
                IsPrintable = false,
                IsLocked = true,
            };

            session.ImportAssets(new Dictionary<Guid, ReadOnlyMemory<byte>> { [assetId] = bytes });
            _ = dispatcher.Execute(new AddBackgroundAssetCommand(asset, background));
            BackgroundStatus = DesktopText.Format("BackgroundImportedFormat", fileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            BackgroundStatus = DesktopText.Format("BackgroundImportFailedFormat", exception.Message);
        }
    }

    public void DeleteActiveBackground()
    {
        if (ActiveBackground is { } background)
        {
            _ = dispatcher.Execute(new DeleteBackgroundAssetCommand(background.Id));
            BackgroundStatus = DesktopText.Get("BackgroundDeleted");
        }
    }

    public void Align(AlignmentMode mode)
    {
        IReadOnlyDictionary<Guid, MmRect> bounds = AlignmentService.Calculate(Document, SelectedElementIds, mode);
        _ = dispatcher.Execute(new ResizeElementsCommand(bounds));
    }

    public void Distribute(DistributionAxis axis)
    {
        IReadOnlyDictionary<Guid, MmRect> bounds = DistributionService.Calculate(Document, SelectedElementIds, axis);
        _ = dispatcher.Execute(new ResizeElementsCommand(bounds));
    }

    public void AddGuide(GuideOrientation orientation, double positionMm)
    {
        GuideDefinition[] guides = Document.Guides.Append(new GuideDefinition(Guid.NewGuid(), orientation, positionMm)).ToArray();
        _ = dispatcher.Execute(new ChangeGuidesCommand(guides));
    }

    public void ClearGuides() => _ = dispatcher.Execute(new ChangeGuidesCommand(Array.Empty<GuideDefinition>()));

    private void OnSessionChanged(object? sender, DocumentSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(SelectedElementIds));
        OnPropertyChanged(nameof(AssetContents));
        NotifyBackgroundPropertiesChanged();
    }

    private BackgroundDefinition? ActiveBackground => Document.Backgrounds.Count == 0
        ? null
        : Document.Backgrounds[^1];

    private void ChangeActiveBackground(
        Func<BackgroundDefinition, BackgroundDefinition> transform,
        string? coalescingKey = null)
    {
        if (ActiveBackground is { } background)
        {
            _ = dispatcher.Execute(new ChangeBackgroundPropertiesCommand(background.Id, transform, coalescingKey));
        }
    }

    private void NotifyBackgroundPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasBackground));
        OnPropertyChanged(nameof(ActiveBackgroundVisible));
        OnPropertyChanged(nameof(ActiveBackgroundPrintable));
        OnPropertyChanged(nameof(ActiveBackgroundLocked));
        OnPropertyChanged(nameof(ActiveBackgroundOpacity));
        OnPropertyChanged(nameof(ActiveBackgroundPdfPageNumber));
        OnPropertyChanged(nameof(ActiveBackgroundFitMode));
    }

    private MmRect NormalizeCreationBounds(MmRect requested)
    {
        MmSize page = Document.Page.Media.Size;
        double width = Math.Clamp(requested.Width, 0.1, page.Width);
        double height = Math.Clamp(requested.Height, 0.1, page.Height);
        double x = Math.Clamp(requested.X, 0, Math.Max(0, page.Width - width));
        double y = Math.Clamp(requested.Y, 0, Math.Max(0, page.Height - height));
        return new MmRect(x, y, width, height);
    }

    private static TemplateElement CreateElementCore(
        DesignerTool tool,
        Guid layerId,
        int zIndex,
        MmRect bounds)
    {
        TemplateElement element = tool switch
        {
            DesignerTool.Text => new TextElement { Content = ElementExpression.Literal(DesktopText.Get("ToolText")) },
            DesignerTool.DateTime => new DateTimeElement(),
            DesignerTool.Serial => new SerialElement(),
            DesignerTool.Image => new ImageElement(),
            DesignerTool.Barcode => new BarcodeElement { Content = ElementExpression.Literal("123456") },
            DesignerTool.QrCode => new BarcodeElement { Symbology = BarcodeSymbology.QrCode, Content = ElementExpression.Literal("RMPP") },
            DesignerTool.DataMatrix => new BarcodeElement { Symbology = BarcodeSymbology.DataMatrix, Content = ElementExpression.Literal("RMPP") },
            DesignerTool.Line => new LineElement { Start = new MmPoint(0, 0), End = new MmPoint(bounds.Width, bounds.Height) },
            DesignerTool.Rectangle => new RectangleElement(),
            DesignerTool.RoundedRectangle => new RectangleElement { CornerRadius = new MmSize(3, 3) },
            DesignerTool.Ellipse => new EllipseElement(),
            DesignerTool.Arc => new ArcElement(),
            DesignerTool.Sector => new SectorElement(),
            DesignerTool.Polyline => new PolylineElement
            {
                Points = [new MmPoint(0, bounds.Height), new MmPoint(bounds.Width / 2, 0), new MmPoint(bounds.Width, bounds.Height)],
            },
            DesignerTool.Polygon => new PolygonElement
            {
                Points = [new MmPoint(bounds.Width / 2, 0), new MmPoint(bounds.Width, bounds.Height), new MmPoint(0, bounds.Height)],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unsupported designer tool."),
        };
        return element with
        {
            Name = DesktopText.Get(ToolItems.Single(item => item.Tool == tool).ResourceKey),
            LayerId = layerId,
            ZIndex = zIndex,
            Bounds = bounds,
        };
    }

    public void Dispose() => session.Changed -= OnSessionChanged;
}
