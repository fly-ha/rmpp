using System.Globalization;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Fills;
using Rmpp.Rendering.Geometry;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Barcodes;
using Rmpp.Rendering.Text;

namespace Rmpp.Rendering.Layout;

public sealed record ElementSceneBuildResult(
    IReadOnlyList<RenderCommand> Commands,
    IReadOnlyList<RenderIssue> Issues);

/// <summary>把单个已解析领域元素转换为一个或多个后端无关命令。</summary>
public class ElementSceneBuilder(
    TextLayoutService? textLayoutService = null,
    BarcodeValidator? barcodeValidator = null)
{
    private readonly TextLayoutService textLayoutService = textLayoutService ?? new TextLayoutService();
    private readonly BarcodeValidator barcodeValidator = barcodeValidator ?? new BarcodeValidator();

    public virtual ElementSceneBuildResult Build(TemplateElement element, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            element.Validate();
            ElementSceneBuildResult result = element switch
            {
                ShapeElement shape => Success(BuildShape(shape, context)),
                TextElement text => BuildTextElement(text, ResolveText(text.Content.Source, text.Id, context), context),
                DateTimeElement dateTime => BuildText(dateTime, ResolveDateTime(dateTime, context), dateTime.TextStyle, context),
                SerialElement serial => BuildText(serial, ResolveSerial(serial, context), serial.TextStyle, context),
                ImageElement image => Success(BuildImage(image, context.Find(image.Id), context)),
                BarcodeElement barcode => BuildBarcode(barcode, ResolveText(barcode.Content.Source, barcode.Id, context), context),
                _ => throw new NotSupportedException($"Unsupported element type: {element.GetType().Name}"),
            };
            return result;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new ElementSceneBuildResult(
                Array.Empty<RenderCommand>(),
                [new RenderIssue("invalid-element", exception.Message, RenderIssueSeverity.Error, element.Id)]);
        }
    }

    private static RenderPathCommand BuildShape(ShapeElement shape, RenderContext context) =>
        new()
        {
            SourceId = shape.Id,
            ZIndex = shape.ZIndex,
            Opacity = shape.Opacity,
            Transform = RenderTransform.ForElement(shape.Bounds, shape.Rotation).Then(context.PlacementTransform),
            Path = ShapePathFactory.Create(shape),
            Stroke = RenderStroke.FromDomain(shape.Stroke),
            Fill = HatchPatternFactory.CreateFill(shape.Fill),
        };

    private ElementSceneBuildResult BuildTextElement(TextElement text, string content, RenderContext context)
    {
        List<RenderCommand> commands = [];
        RenderStroke border = RenderStroke.FromDomain(text.Border);
        RenderFill fill = HatchPatternFactory.CreateFill(text.Fill);
        if (border.IsEnabled || fill is not RenderNoFill)
        {
            RectangleElement box = new() { Bounds = new MmRect(0, 0, text.Bounds.Width, text.Bounds.Height) };
            commands.Add(new RenderPathCommand
            {
                SourceId = text.Id,
                ZIndex = text.ZIndex,
                Opacity = text.Opacity,
                Transform = RenderTransform.ForElement(text.Bounds, text.Rotation).Then(context.PlacementTransform),
                Path = ShapePathFactory.Create(box),
                Stroke = border,
                Fill = fill,
            });
        }

        ElementSceneBuildResult textResult = BuildText(text, content, text.TextStyle, context);
        commands.AddRange(textResult.Commands);
        return new ElementSceneBuildResult(commands, textResult.Issues);
    }

    private ElementSceneBuildResult BuildText(
        TemplateElement element,
        string content,
        TextStyle style,
        RenderContext context)
    {
        MmRect localBounds = new(0, 0, element.Bounds.Width, element.Bounds.Height);
        TextLayoutResult layout = textLayoutService.Layout(new TextLayoutRequest
        {
            Text = content,
            Bounds = localBounds,
            Style = RenderTextStyle.FromDomain(style),
        });
        RenderTextCommand command = new()
        {
            SourceId = element.Id,
            ZIndex = element.ZIndex,
            Opacity = element.Opacity,
            Transform = RenderTransform.ForElement(element.Bounds, element.Rotation).Then(context.PlacementTransform),
            Clip = RenderClip.FromRectangle(localBounds),
            Text = content,
            LocalBounds = localBounds,
            Style = RenderTextStyle.FromDomain(style) with { FontSizePoints = layout.ActualFontSizePoints },
            GlyphRuns = layout.GlyphRuns,
        };
        RenderIssue[] issues = layout.Issues.Select(issue => new RenderIssue(
            issue.Code,
            issue.Message,
            issue.Severity switch
            {
                TextLayoutIssueSeverity.Information => RenderIssueSeverity.Information,
                TextLayoutIssueSeverity.Error => RenderIssueSeverity.Error,
                _ => RenderIssueSeverity.Warning,
            },
            element.Id)).ToArray();
        return new ElementSceneBuildResult([command], issues);
    }

    private static RenderImageCommand BuildImage(
        ImageElement image,
        ResolvedElement? resolved,
        RenderContext context)
    {
        MmRect localBounds = new(0, 0, image.Bounds.Width, image.Bounds.Height);
        return new RenderImageCommand
        {
            SourceId = image.Id,
            ZIndex = image.ZIndex,
            Opacity = image.Opacity,
            Transform = RenderTransform.ForElement(image.Bounds, image.Rotation).Then(context.PlacementTransform),
            Clip = RenderClip.FromRectangle(localBounds),
            LocalBounds = localBounds,
            Image = new RenderImage
            {
                AssetId = resolved?.AssetId ?? image.AssetId,
                LocalPath = resolved?.LocalImagePath,
                FitMode = image.FitMode,
                Crop = image.Crop,
            },
            Border = RenderStroke.FromDomain(image.Border),
            Fill = HatchPatternFactory.CreateFill(image.Fill),
        };
    }

    private ElementSceneBuildResult BuildBarcode(BarcodeElement barcode, string content, RenderContext context)
    {
        MmRect localBounds = new(0, 0, barcode.Bounds.Width, barcode.Bounds.Height);
        BarcodeValidationResult validation = barcodeValidator.Validate(new BarcodeOptions
        {
            Symbology = barcode.Symbology,
            Content = content,
            QuietZoneMm = barcode.QuietZoneMm,
            ErrorCorrectionLevel = barcode.ErrorCorrectionLevel,
            ShowHumanReadableText = barcode.ShowHumanReadableText,
        });
        if (!validation.IsValid)
        {
            return new ElementSceneBuildResult(
                Array.Empty<RenderCommand>(),
                validation.Issues.Select(issue => new RenderIssue(
                    issue.Code,
                    issue.Message,
                    RenderIssueSeverity.Error,
                    barcode.Id)).ToArray());
        }

        RenderBarcodeCommand command = new()
        {
            SourceId = barcode.Id,
            ZIndex = barcode.ZIndex,
            Opacity = barcode.Opacity,
            Transform = RenderTransform.ForElement(barcode.Bounds, barcode.Rotation).Then(context.PlacementTransform),
            Clip = RenderClip.FromRectangle(localBounds),
            Content = content,
            Symbology = barcode.Symbology,
            LocalBounds = localBounds,
            QuietZoneMm = barcode.QuietZoneMm,
            ErrorCorrectionLevel = barcode.ErrorCorrectionLevel,
            ShowHumanReadableText = barcode.ShowHumanReadableText,
            HumanReadableTextStyle = RenderTextStyle.FromDomain(barcode.HumanReadableTextStyle),
        };
        return Success(command);
    }

    private static ElementSceneBuildResult Success(RenderCommand command) =>
        new([command], Array.Empty<RenderIssue>());

    private static string ResolveText(string fallback, Guid elementId, RenderContext context) =>
        context.Find(elementId)?.Text ?? fallback;

    private static string ResolveDateTime(DateTimeElement element, RenderContext context)
    {
        if (context.Find(element.Id)?.Text is { } resolved)
        {
            return resolved;
        }

        CultureInfo culture = string.IsNullOrWhiteSpace(element.Definition.CultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(element.Definition.CultureName);
        return context.ReferenceTime.ToString(element.Definition.Format, culture);
    }

    private static string ResolveSerial(SerialElement element, RenderContext context)
    {
        if (context.Find(element.Id)?.Text is { } resolved)
        {
            return resolved;
        }

        string number = element.Definition.MinimumDigits > 0
            ? element.Definition.Start.ToString($"D{element.Definition.MinimumDigits}", CultureInfo.InvariantCulture)
            : element.Definition.Start.ToString(CultureInfo.InvariantCulture);
        return element.Definition.Prefix + number + element.Definition.Suffix;
    }
}
