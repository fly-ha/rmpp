using CommunityToolkit.Mvvm.ComponentModel;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Data;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.Utilities;
using Rmpp.Rendering.Text;

namespace Rmpp.Desktop.ViewModels;

/// <summary>为全部首版元素提供公共几何/状态及类型相关内容、条码、图片和描边填充属性。</summary>
public sealed class PropertiesViewModel : ObservableObject, IDisposable
{
    private readonly DocumentSession session;
    private readonly EditorCommandDispatcher dispatcher;
    private readonly Array barcodeSymbologies = Enum.GetValues<BarcodeSymbology>();
    private readonly Array imageFitModes = Enum.GetValues<ImageFitMode>();
    private readonly Array hatchPatterns = Enum.GetValues<HatchPattern>();
    private readonly IReadOnlyList<string> fontFamilies;
    private static readonly Lazy<IReadOnlyList<string>> InstalledFontFamilies = new(
        static () => new LocalFontCatalog().Families.ToArray());

    public PropertiesViewModel(DocumentSession session, EditorCommandDispatcher dispatcher)
    {
        this.session = session;
        this.dispatcher = dispatcher;
        fontFamilies = InstalledFontFamilies.Value;
        session.Changed += OnSessionChanged;
    }

    public TemplateElement? SelectedElement => session.State.Document.Elements.FirstOrDefault(
        element => session.State.SelectedElementIds.Contains(element.Id));
    public Array BarcodeSymbologies => barcodeSymbologies;
    public Array ImageFitModes => imageFitModes;
    public Array HatchPatterns => hatchPatterns;
    public IReadOnlyList<string> FontFamilies => fontFamilies;

    public string TypeName => SelectedElement?.GetType().Name ?? DesktopText.Get("NotSelected");
    public bool HasSelection => SelectedElement is not null;
    public bool IsTextElement => SelectedElement is TextElement;
    public bool IsDateTimeElement => SelectedElement is DateTimeElement;
    public bool IsSerialElement => SelectedElement is SerialElement;
    public bool IsBarcodeElement => SelectedElement is BarcodeElement;
    public bool IsQrCode => SelectedElement is BarcodeElement { Symbology: BarcodeSymbology.QrCode };
    public bool IsImageElement => SelectedElement is ImageElement;
    public bool SupportsPointEditing => SelectedElement is LineElement or PolylineElement or PolygonElement;
    public bool SupportsArcEditing => SelectedElement is ArcElement or SectorElement;
    public bool SupportsCornerRadius => SelectedElement is RectangleElement;
    public bool SupportsFont => SelectedElement is TextElement or DateTimeElement or SerialElement;
    public bool SupportsStroke => SelectedElement is ShapeElement or TextElement or ImageElement;
    public bool SupportsFill => SelectedElement is RectangleElement or EllipseElement or SectorElement or PolygonElement or TextElement or ImageElement;
    public string? Name { get => SelectedElement?.Name; set => Change(element => element with { Name = value ?? string.Empty }, "SetName"); }
    public double? X { get => SelectedElement?.Bounds.X; set => SetBounds(value, static (bounds, v) => new MmRect(v, bounds.Y, bounds.Width, bounds.Height)); }
    public double? Y { get => SelectedElement?.Bounds.Y; set => SetBounds(value, static (bounds, v) => new MmRect(bounds.X, v, bounds.Width, bounds.Height)); }
    public double? Width { get => SelectedElement?.Bounds.Width; set => SetBounds(value, static (bounds, v) => new MmRect(bounds.X, bounds.Y, v, bounds.Height), positive: true); }
    public double? Height { get => SelectedElement?.Bounds.Height; set => SetBounds(value, static (bounds, v) => new MmRect(bounds.X, bounds.Y, bounds.Width, v), positive: true); }
    public double? Rotation { get => SelectedElement?.Rotation.Degrees; set => Change(element => element with { Rotation = new Angle(value ?? 0) }, "SetRotation"); }
    public double? Opacity { get => SelectedElement?.Opacity; set => Change(element => element with { Opacity = Math.Clamp(value ?? 1, 0.01, 1) }, "SetOpacity"); }
    public bool? IsVisible { get => SelectedElement?.IsVisible; set => Change(element => element with { IsVisible = value ?? true }, "SetVisibility"); }
    public bool? IsPrintable { get => SelectedElement?.IsPrintable; set => Change(element => element with { IsPrintable = value ?? true }, "SetPrintable"); }
    public bool? IsLocked { get => SelectedElement?.IsLocked; set => ChangeState(value); }

    public string? Content
    {
        get => SelectedElement switch
        {
            TextElement text => text.Content.Source,
            BarcodeElement barcode => barcode.Content.Source,
            ImageElement image => image.VariablePath?.Source,
            DateTimeElement dateTime => dateTime.Definition.Format,
            SerialElement serial => serial.Definition.Prefix + "{n}" + serial.Definition.Suffix,
            _ => null,
        };
        set => Change(element => element switch
        {
            TextElement text => text with { Content = ElementExpression.Literal(value ?? string.Empty) },
            BarcodeElement barcode => barcode with { Content = new ElementExpression(value ?? string.Empty) },
            ImageElement image => image with { VariablePath = string.IsNullOrWhiteSpace(value) ? null : new ElementExpression(value) },
            DateTimeElement dateTime => dateTime with { Definition = dateTime.Definition with { Format = value ?? string.Empty } },
            SerialElement serial => serial with { Definition = serial.Definition with { Prefix = value ?? string.Empty } },
            _ => element,
        }, "SetContent");
    }

    public string? DateTimeFormat
    {
        get => (SelectedElement as DateTimeElement)?.Definition.Format;
        set => Change(element => element is DateTimeElement dateTime
            ? dateTime with { Definition = dateTime.Definition with { Format = value ?? string.Empty } }
            : element, "SetContent");
    }

    public string? SerialPrefix
    {
        get => (SelectedElement as SerialElement)?.Definition.Prefix;
        set => Change(element => element is SerialElement serial
            ? serial with { Definition = serial.Definition with { Prefix = value ?? string.Empty } }
            : element, "SetContent");
    }

    public string? SerialSuffix
    {
        get => (SelectedElement as SerialElement)?.Definition.Suffix;
        set => Change(element => element is SerialElement serial
            ? serial with { Definition = serial.Definition with { Suffix = value ?? string.Empty } }
            : element, "SetContent");
    }

    public int? SerialMinimumDigits
    {
        get => (SelectedElement as SerialElement)?.Definition.MinimumDigits;
        set => Change(element => element is SerialElement serial
            ? serial with { Definition = serial.Definition with { MinimumDigits = Math.Max(0, value ?? 0) } }
            : element, "SetContent");
    }

    public string? ImageFileName => SelectedElement is ImageElement { AssetId: { } assetId }
        ? session.State.Document.Assets.FirstOrDefault(asset => asset.Id == assetId)?.FileName
        : null;
    public string ImageDisplayName => ImageFileName
        ?? (SelectedElement as ImageElement)?.VariablePath?.Source
        ?? DesktopText.Get("ImageNotSelected");
    public bool HasImageAsset => SelectedElement is ImageElement { AssetId: not null }
        or ImageElement { VariablePath: not null };

    public BarcodeSymbology? Symbology
    {
        get => (SelectedElement as BarcodeElement)?.Symbology;
        set
        {
            if (SelectedElement is BarcodeElement barcode && value is not null)
            {
                _ = dispatcher.Execute(new SetBarcodeSymbologyCommand(barcode.Id, value.Value));
            }
        }
    }

    public string? BarcodeCenterIconFileName => SelectedElement is BarcodeElement { CenterIconAssetId: { } assetId }
        ? session.State.Document.Assets.FirstOrDefault(asset => asset.Id == assetId)?.FileName
        : null;
    public string BarcodeCenterIconDisplayName => BarcodeCenterIconFileName ?? DesktopText.Get("QrCenterIconNotSelected");
    public bool HasBarcodeCenterIcon => SelectedElement is BarcodeElement { CenterIconAssetId: not null };
    public double? BarcodeCenterIconScale
    {
        get => (SelectedElement as BarcodeElement)?.CenterIconScale;
        set
        {
            if (value is { } scale
                && double.IsFinite(scale)
                && scale >= BarcodeElement.MinimumCenterIconScale
                && scale <= BarcodeElement.MaximumCenterIconScale)
            {
                Change(element => element is BarcodeElement barcode
                    ? barcode with { CenterIconScale = scale }
                    : element, "SetBarcode");
            }
        }
    }

    public ImageFitMode? ImageFit
    {
        get => (SelectedElement as ImageElement)?.FitMode;
        set => Change(element => element is ImageElement image && value is not null ? image with { FitMode = value.Value } : element, "SetImageFit");
    }

    public double? StrokeWidth
    {
        get => GetStroke(SelectedElement)?.WidthMm;
        set => Change(element => SetStroke(element, (GetStroke(element) ?? new StrokeStyle()) with
        {
            IsEnabled = (value ?? 0) > 0,
            WidthMm = Math.Max(0, value ?? 0),
        }), "SetStroke");
    }

    public string FillMode
    {
        get => GetFill(SelectedElement) switch { SolidFill => "Solid", HatchFill => "Hatch", _ => "None" };
        set => Change(element => SetFill(element, value switch
        {
            "Solid" => new SolidFill(new RgbaColor(230, 230, 230)),
            "Hatch" => new HatchFill(),
            _ => FillStyle.None,
        }), "SetFill");
    }

    public HatchPattern? HatchPattern
    {
        get => (GetFill(SelectedElement) as HatchFill)?.Pattern;
        set => Change(element => SetFill(element, value is null
            ? GetFill(element)
            : (GetFill(element) as HatchFill ?? new HatchFill()) with { Pattern = value.Value }), "SetHatch");
    }

    public bool HasSolidFill => GetFill(SelectedElement) is SolidFill;
    public bool HasHatchFill => GetFill(SelectedElement) is HatchFill;

    public string? Points
    {
        get => SelectedElement switch
        {
            LineElement line => FormatPoints([line.Start, line.End]),
            PolylineElement polyline => FormatPoints(polyline.Points),
            PolygonElement polygon => FormatPoints(polygon.Points),
            _ => null,
        };
        set
        {
            TemplateElement? element = SelectedElement;
            if (element is null || !TryParsePoints(value, out MmPoint[] points))
            {
                return;
            }

            _ = dispatcher.Execute(new EditPointsCommand(element.Id, points, "property-points"));
        }
    }

    public string? FontFamily
    {
        get => GetTextStyle(SelectedElement)?.FontFamily;
        set => Change(element => SetTextStyle(element, (GetTextStyle(element) ?? new TextStyle()) with { FontFamily = value ?? string.Empty }), "SetContent");
    }

    public double? FontSize
    {
        get => GetTextStyle(SelectedElement)?.FontSizePoints;
        set => Change(element => SetTextStyle(element, (GetTextStyle(element) ?? new TextStyle()) with { FontSizePoints = Math.Max(0.1, value ?? 10) }), "SetContent");
    }

    public bool? FontBold
    {
        get => GetTextStyle(SelectedElement)?.IsBold;
        set => Change(element => SetTextStyle(element, (GetTextStyle(element) ?? new TextStyle()) with { IsBold = value ?? false }), "SetContent");
    }

    public bool? FontItalic
    {
        get => GetTextStyle(SelectedElement)?.IsItalic;
        set => Change(element => SetTextStyle(element, (GetTextStyle(element) ?? new TextStyle()) with { IsItalic = value ?? false }), "SetContent");
    }

    public string TextColor
    {
        get => RgbaColorText.Format(GetTextStyle(SelectedElement)?.Color ?? RgbaColor.Black);
        set
        {
            if (RgbaColorText.TryParse(value, out RgbaColor color))
            {
                Change(element => SetTextStyle(element, (GetTextStyle(element) ?? new TextStyle()) with { Color = color }), "SetContent");
            }
        }
    }

    public string StrokeColor
    {
        get => RgbaColorText.Format(GetStroke(SelectedElement)?.Color ?? RgbaColor.Black);
        set
        {
            if (RgbaColorText.TryParse(value, out RgbaColor color))
            {
                Change(element => SetStroke(element, (GetStroke(element) ?? new StrokeStyle()) with { Color = color }), "SetStroke");
            }
        }
    }

    public string SolidFillColor
    {
        get => RgbaColorText.Format((GetFill(SelectedElement) as SolidFill)?.Color ?? new RgbaColor(230, 230, 230));
        set
        {
            if (RgbaColorText.TryParse(value, out RgbaColor color))
            {
                Change(element => SetFill(element, new SolidFill(color)), "SetFill");
            }
        }
    }

    public string HatchForegroundColor
    {
        get => RgbaColorText.Format((GetFill(SelectedElement) as HatchFill)?.Foreground ?? RgbaColor.Black);
        set
        {
            if (RgbaColorText.TryParse(value, out RgbaColor color))
            {
                Change(element => SetFill(element, (GetFill(element) as HatchFill ?? new HatchFill()) with { Foreground = color }), "SetHatch");
            }
        }
    }

    public string HatchBackgroundColor
    {
        get => RgbaColorText.Format((GetFill(SelectedElement) as HatchFill)?.Background ?? RgbaColor.Transparent);
        set
        {
            if (RgbaColorText.TryParse(value, out RgbaColor color))
            {
                Change(element => SetFill(element, (GetFill(element) as HatchFill ?? new HatchFill()) with { Background = color }), "SetHatch");
            }
        }
    }

    public double? StartAngle
    {
        get => SelectedElement switch { ArcElement arc => arc.StartAngle.Degrees, SectorElement sector => sector.StartAngle.Degrees, _ => null };
        set => Change(element => element switch
        {
            ArcElement arc => arc with { StartAngle = new Angle(value ?? 0) },
            SectorElement sector => sector with { StartAngle = new Angle(value ?? 0) },
            _ => element,
        }, "SetRotation");
    }

    public double? SweepDegrees
    {
        get => SelectedElement switch { ArcElement arc => arc.SweepDegrees, SectorElement sector => sector.SweepDegrees, _ => null };
        set => Change(element => element switch
        {
            ArcElement arc => arc with { SweepDegrees = value ?? 90 },
            SectorElement sector => sector with { SweepDegrees = value ?? 90 },
            _ => element,
        }, "SetRotation");
    }

    public double? CornerRadius
    {
        get => (SelectedElement as RectangleElement)?.CornerRadius.Width;
        set => Change(element => element is RectangleElement rectangle
            ? rectangle with { CornerRadius = new MmSize(Math.Max(0, value ?? 0), Math.Max(0, value ?? 0)) }
            : element, "SetSize");
    }

    public long? SerialStart
    {
        get => (SelectedElement as SerialElement)?.Definition.Start;
        set => Change(element => element is SerialElement serial ? serial with { Definition = serial.Definition with { Start = value ?? 1 } } : element, "SetContent");
    }

    public long? SerialStep
    {
        get => (SelectedElement as SerialElement)?.Definition.Step;
        set => Change(element => element is SerialElement serial && value is not null && value != 0
            ? serial with { Definition = serial.Definition with { Step = value.Value } }
            : element, "SetContent");
    }

    public double? SerialStartValue
    {
        get => SerialStart;
        set
        {
            if (value is { } number && double.IsFinite(number)) SerialStart = checked((long)number);
        }
    }

    public double? SerialStepValue
    {
        get => SerialStep;
        set
        {
            if (value is { } number && double.IsFinite(number) && number != 0) SerialStep = checked((long)number);
        }
    }

    public double? SerialMinimumDigitsValue
    {
        get => SerialMinimumDigits;
        set
        {
            if (value is { } number && double.IsFinite(number)) SerialMinimumDigits = checked((int)number);
        }
    }

    public string? ImageCrop
    {
        get => (SelectedElement as ImageElement)?.Crop is { } crop
            ? $"{crop.X:0.###},{crop.Y:0.###},{crop.Width:0.###},{crop.Height:0.###}"
            : null;
        set
        {
            if (TryParseRect(value, out MmRect? crop))
            {
                Change(element => element is ImageElement image ? image with { Crop = crop } : element, "SetImageFit");
            }
        }
    }

    public bool HasImageCrop => SelectedElement is ImageElement { Crop: not null };
    public double? ImageCropX { get => (SelectedElement as ImageElement)?.Crop?.X; set => SetImageCrop(value, static (crop, number) => new MmRect(number, crop.Y, crop.Width, crop.Height)); }
    public double? ImageCropY { get => (SelectedElement as ImageElement)?.Crop?.Y; set => SetImageCrop(value, static (crop, number) => new MmRect(crop.X, number, crop.Width, crop.Height)); }
    public double? ImageCropWidth { get => (SelectedElement as ImageElement)?.Crop?.Width; set => SetImageCrop(value, static (crop, number) => new MmRect(crop.X, crop.Y, number, crop.Height), positive: true); }
    public double? ImageCropHeight { get => (SelectedElement as ImageElement)?.Crop?.Height; set => SetImageCrop(value, static (crop, number) => new MmRect(crop.X, crop.Y, crop.Width, number), positive: true); }

    public void ClearImageCrop() => Change(element => element is ImageElement image ? image with { Crop = null } : element, "SetImageFit");

    public string ValidationSummary => SelectedElement is null
        ? string.Empty
        : string.Join("；", session.State.ValidationIssues
            .Where(issue => issue.Location.ElementId == SelectedElement.Id)
            .Select(static issue => issue.Message));

    public void Dispose() => session.Changed -= OnSessionChanged;

    private void SetBounds(double? value, Func<MmRect, double, MmRect> transform, bool positive = false)
    {
        if (value is null || !double.IsFinite(value.Value) || (positive && value <= 0))
        {
            return;
        }

        Change(element => element with { Bounds = transform(element.Bounds, value.Value) }, "SetSize");
    }

    private void Change(Func<TemplateElement, TemplateElement> transform, string description)
    {
        TemplateElement? element = SelectedElement;
        if (element is not null)
        {
            _ = dispatcher.Execute(new ChangeElementPropertiesCommand([element.Id], transform, DesktopText.Get(description), "properties"));
        }
    }

    private void ChangeState(bool? locked)
    {
        TemplateElement? element = SelectedElement;
        if (element is not null)
        {
            _ = dispatcher.Execute(new SetElementStateCommand([element.Id], isLocked: locked ?? false));
        }
    }

    private static StrokeStyle? GetStroke(TemplateElement? element) => element switch
    {
        ShapeElement shape => shape.Stroke,
        TextElement text => text.Border,
        ImageElement image => image.Border,
        _ => null,
    };

    private static TemplateElement SetStroke(TemplateElement element, StrokeStyle stroke) => element switch
    {
        ShapeElement shape => shape with { Stroke = stroke },
        TextElement text => text with { Border = stroke },
        ImageElement image => image with { Border = stroke },
        _ => element,
    };

    private static FillStyle? GetFill(TemplateElement? element) => element switch
    {
        ShapeElement shape => shape.Fill,
        TextElement text => text.Fill,
        ImageElement image => image.Fill,
        _ => null,
    };

    private static TemplateElement SetFill(TemplateElement element, FillStyle? fill) => element switch
    {
        ShapeElement shape => shape with { Fill = fill ?? FillStyle.None },
        TextElement text => text with { Fill = fill ?? FillStyle.None },
        ImageElement image => image with { Fill = fill ?? FillStyle.None },
        _ => element,
    };

    private void OnSessionChanged(object? sender, DocumentSessionChangedEventArgs e)
    {
        foreach (string property in new[]
        {
            nameof(SelectedElement), nameof(TypeName), nameof(Name), nameof(X), nameof(Y), nameof(Width), nameof(Height),
            nameof(HasSelection), nameof(IsTextElement), nameof(IsDateTimeElement), nameof(IsSerialElement),
            nameof(IsBarcodeElement), nameof(IsQrCode), nameof(IsImageElement), nameof(SupportsPointEditing), nameof(SupportsArcEditing),
            nameof(SupportsCornerRadius), nameof(SupportsFont), nameof(SupportsStroke), nameof(SupportsFill),
            nameof(Rotation), nameof(Opacity), nameof(IsVisible), nameof(IsPrintable), nameof(IsLocked), nameof(Content),
            nameof(Symbology), nameof(BarcodeCenterIconFileName), nameof(BarcodeCenterIconDisplayName), nameof(HasBarcodeCenterIcon), nameof(BarcodeCenterIconScale),
            nameof(ImageFit), nameof(StrokeWidth), nameof(StrokeColor), nameof(FillMode), nameof(HatchPattern),
            nameof(HasSolidFill), nameof(HasHatchFill), nameof(SolidFillColor), nameof(HatchForegroundColor), nameof(HatchBackgroundColor), nameof(ValidationSummary),
            nameof(Points),
            nameof(FontFamily), nameof(FontSize), nameof(FontBold), nameof(FontItalic), nameof(TextColor), nameof(StartAngle), nameof(SweepDegrees), nameof(CornerRadius),
            nameof(SerialStart), nameof(SerialStep), nameof(SerialStartValue), nameof(SerialStepValue), nameof(SerialMinimumDigitsValue),
            nameof(SerialPrefix), nameof(SerialSuffix), nameof(SerialMinimumDigits),
            nameof(DateTimeFormat), nameof(ImageCrop), nameof(HasImageCrop), nameof(ImageCropX), nameof(ImageCropY), nameof(ImageCropWidth), nameof(ImageCropHeight),
            nameof(ImageFileName), nameof(ImageDisplayName), nameof(HasImageAsset),
        })
        {
            OnPropertyChanged(property);
        }
    }

    private static string FormatPoints(IEnumerable<MmPoint> points) =>
        string.Join("; ", points.Select(static point => $"{point.X:0.###},{point.Y:0.###}"));

    private static bool TryParsePoints(string? value, out MmPoint[] points)
    {
        List<MmPoint> parsed = [];
        foreach (string pair in (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] coordinates = pair.Split(',', StringSplitOptions.TrimEntries);
            if (coordinates.Length != 2
                || !double.TryParse(coordinates[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double x)
                || !double.TryParse(coordinates[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double y))
            {
                points = [];
                return false;
            }
            parsed.Add(new MmPoint(x, y));
        }

        points = parsed.ToArray();
        return points.Length >= 2;
    }

    private static TextStyle? GetTextStyle(TemplateElement? element) => element switch
    {
        TextElement text => text.TextStyle,
        DateTimeElement dateTime => dateTime.TextStyle,
        SerialElement serial => serial.TextStyle,
        BarcodeElement barcode => barcode.HumanReadableTextStyle,
        _ => null,
    };

    private static TemplateElement SetTextStyle(TemplateElement element, TextStyle style) => element switch
    {
        TextElement text => text with { TextStyle = style },
        DateTimeElement dateTime => dateTime with { TextStyle = style },
        SerialElement serial => serial with { TextStyle = style },
        BarcodeElement barcode => barcode with { HumanReadableTextStyle = style },
        _ => element,
    };

    private static bool TryParseRect(string? value, out MmRect? rectangle)
    {
        string[] parts = (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(value))
        {
            rectangle = null;
            return true;
        }

        if (parts.Length == 4
            && parts.Select(part => double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out _)).All(static success => success))
        {
            double[] values = parts.Select(part => double.Parse(part, System.Globalization.CultureInfo.CurrentCulture)).ToArray();
            rectangle = new MmRect(values[0], values[1], Math.Max(0, values[2]), Math.Max(0, values[3]));
            return true;
        }

        rectangle = null;
        return false;
    }

    private void SetImageCrop(double? value, Func<MmRect, double, MmRect> transform, bool positive = false)
    {
        if (SelectedElement is not ImageElement image
            || value is null
            || !double.IsFinite(value.Value)
            || positive && value.Value <= 0)
        {
            return;
        }

        MmRect current = image.Crop ?? new MmRect(0, 0, image.Bounds.Width, image.Bounds.Height);
        Change(element => element is ImageElement currentImage
            ? currentImage with { Crop = transform(current, value.Value) }
            : element, "SetImageFit");
    }
}
