using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Rendering.Tests.Layout;

public sealed class RenderSceneBuilderTests
{
    [Fact]
    public void EditorAndPrintTargetsApplyVisibilityAndPrintabilityDeterministically()
    {
        LayerDefinition printableLayer = new(Guid.NewGuid(), "Printable");
        LayerDefinition screenOnlyLayer = new(Guid.NewGuid(), "Screen only") { IsPrintable = false };
        Guid backgroundId = Guid.NewGuid();
        RectangleElement high = Rectangle(printableLayer.Id, "high", zIndex: 10);
        RectangleElement firstLow = Rectangle(printableLayer.Id, "first-low", zIndex: 1);
        RectangleElement secondLow = Rectangle(screenOnlyLayer.Id, "second-low", zIndex: 1);
        RectangleElement hidden = Rectangle(printableLayer.Id, "hidden", zIndex: 0) with { IsVisible = false };
        TemplateDocument document = CreateDocument(
            [printableLayer, screenOnlyLayer],
            [high, firstLow, secondLow, hidden],
            [new BackgroundDefinition
            {
                Id = backgroundId,
                AssetId = Guid.NewGuid(),
                Bounds = new MmRect(0, 0, 210, 297),
                IsPrintable = false,
            }]);

        RenderPage editor = Assert.Single(new RenderSceneBuilder().Build(document, new RenderContext
        {
            Target = RenderTarget.Editor,
        }).Pages);
        RenderPage print = Assert.Single(new RenderSceneBuilder().Build(document, new RenderContext
        {
            Target = RenderTarget.Print,
        }).Pages);

        Assert.Equal([backgroundId, firstLow.Id, secondLow.Id, high.Id], editor.Commands.Select(static command => command.SourceId));
        Assert.Equal([firstLow.Id, high.Id], print.Commands.Select(static command => command.SourceId));
    }

    [Fact]
    public void LandscapePageRetainsPhysicalSizeAndExplicitPageClip()
    {
        TemplateDocument source = TemplateDocument.CreateNew();
        TemplateDocument document = source with
        {
            Page = source.Page with
            {
                Media = new MediaDefinition("A4", new MmSize(210, 297), PageOrientation.Landscape),
            },
        };

        RenderPage page = Assert.Single(new RenderSceneBuilder().Build(document).Pages);

        Assert.Equal(new MmSize(297, 210), page.Size);
        Assert.Equal(new MmRect(0, 0, 297, 210), page.Clip.Rectangle);
    }

    [Fact]
    public void ResolvedValuesProduceSemanticTextImageAndBarcodeCommandsWithLocalClips()
    {
        LayerDefinition layer = new(Guid.NewGuid(), "Default");
        TextElement text = new()
        {
            LayerId = layer.Id,
            Bounds = new MmRect(10, 20, 40, 12),
            Content = ElementExpression.Literal("fallback"),
        };
        ImageElement image = new()
        {
            LayerId = layer.Id,
            Bounds = new MmRect(20, 40, 30, 20),
            AssetId = Guid.NewGuid(),
        };
        BarcodeElement barcode = new()
        {
            LayerId = layer.Id,
            Bounds = new MmRect(20, 70, 50, 20),
            Content = ElementExpression.Literal("old"),
        };
        TemplateDocument document = CreateDocument([layer], [text, image, barcode]);
        RenderContext context = new()
        {
            ResolvedElements = new Dictionary<Guid, ResolvedElement>
            {
                [text.Id] = new ResolvedElement { ElementId = text.Id, Text = "resolved text" },
                [image.Id] = new ResolvedElement { ElementId = image.Id, LocalImagePath = "C:\\offline\\logo.png" },
                [barcode.Id] = new ResolvedElement { ElementId = barcode.Id, Text = "123456" },
            },
        };

        RenderCommand[] commands = Assert.Single(new RenderSceneBuilder().Build(document, context).Pages).Commands.ToArray();

        RenderTextCommand textCommand = Assert.IsType<RenderTextCommand>(commands[0]);
        RenderImageCommand imageCommand = Assert.IsType<RenderImageCommand>(commands[1]);
        RenderBarcodeCommand barcodeCommand = Assert.IsType<RenderBarcodeCommand>(commands[2]);
        Assert.Equal("resolved text", textCommand.Text);
        Assert.Equal("C:\\offline\\logo.png", imageCommand.Image.LocalPath);
        Assert.Equal("123456", barcodeCommand.Content);
        Assert.NotNull(textCommand.Clip?.Rectangle);
        Assert.NotNull(imageCommand.Clip?.Rectangle);
        Assert.NotNull(barcodeCommand.Clip?.Rectangle);
    }

    [Fact]
    public void RotationAndLocalGeometryRemainSeparateInSceneCommand()
    {
        LayerDefinition layer = new(Guid.NewGuid(), "Default");
        PolygonElement polygon = new()
        {
            LayerId = layer.Id,
            Bounds = new MmRect(100, 50, 20, 10),
            Rotation = new Angle(90),
            Points = [new MmPoint(0, 0), new MmPoint(20, 0), new MmPoint(10, 10)],
        };

        RenderPathCommand command = Assert.IsType<RenderPathCommand>(
            Assert.Single(Assert.Single(new RenderSceneBuilder().Build(CreateDocument([layer], [polygon])).Pages).Commands));

        Assert.Equal(new MmPoint(115, 45), command.Transform.Transform(new MmPoint(0, 0)));
        Assert.Equal(new MmPoint(0, 0), Assert.IsType<RenderMoveTo>(command.Path.Segments[0]).Point);
    }

    [Fact]
    public void InvalidGeometryAndMissingLayerBecomeStructuredIssues()
    {
        LayerDefinition layer = new(Guid.NewGuid(), "Default");
        PolygonElement invalid = new()
        {
            LayerId = layer.Id,
            Bounds = new MmRect(0, 0, 20, 10),
            Points = [new MmPoint(0, 0), new MmPoint(10, 0), new MmPoint(20, 0)],
        };
        RectangleElement missingLayer = Rectangle(Guid.NewGuid(), "missing", 2);

        RenderPage page = Assert.Single(new RenderSceneBuilder().Build(CreateDocument([layer], [invalid, missingLayer])).Pages);

        Assert.Empty(page.Commands);
        Assert.Contains(page.Issues, static issue => issue.Code == "invalid-element");
        Assert.Contains(page.Issues, static issue => issue.Code == "missing-layer");
    }

    [Fact]
    public void ExplicitPlacementTransformOffsetsCommandsWithoutChangingPageSize()
    {
        LayerDefinition layer = new(Guid.NewGuid(), "Default");
        RectangleElement rectangle = Rectangle(layer.Id, "placed", 0) with
        {
            Bounds = new MmRect(10, 20, 10, 10),
        };
        RenderContext context = new()
        {
            PlacementTransform = RenderTransform.Translation(30, 40),
        };

        RenderPage page = Assert.Single(new RenderSceneBuilder().Build(CreateDocument([layer], [rectangle]), context).Pages);
        RenderPathCommand command = Assert.IsType<RenderPathCommand>(Assert.Single(page.Commands));

        Assert.Equal(new MmSize(210, 297), page.Size);
        Assert.Equal(new MmPoint(40, 60), command.Transform.Transform(new MmPoint(0, 0)));
    }

    private static RectangleElement Rectangle(Guid layerId, string name, int zIndex) =>
        new()
        {
            Name = name,
            LayerId = layerId,
            ZIndex = zIndex,
            Bounds = new MmRect(0, 0, 10, 10),
        };

    private static TemplateDocument CreateDocument(
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<TemplateElement> elements,
        IReadOnlyList<BackgroundDefinition>? backgrounds = null)
    {
        TemplateDocument source = TemplateDocument.CreateNew();
        return source with
        {
            Layers = layers,
            Elements = elements,
            Backgrounds = backgrounds ?? Array.Empty<BackgroundDefinition>(),
        };
    }
}
