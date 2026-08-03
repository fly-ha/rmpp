using System.IO;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Desktop.Tests.ViewModels;

public sealed class TypeSpecificPropertiesTests
{
    [Fact]
    public void PropertyGroupsFollowTheSelectedElementType()
    {
        TemplateDocument document = TemplateDocument.CreateNew("分类属性");
        Guid layerId = document.Layers[0].Id;
        TextElement text = new() { LayerId = layerId, Bounds = new MmRect(0, 0, 30, 10) };
        ImageElement image = new() { LayerId = layerId, Bounds = new MmRect(0, 15, 30, 20) };
        PolygonElement polygon = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(0, 40, 30, 20),
            Points = [new MmPoint(15, 0), new MmPoint(30, 20), new MmPoint(0, 20)],
        };
        ArcElement arc = new() { LayerId = layerId, Bounds = new MmRect(40, 0, 20, 20) };
        document = document with { Elements = [text, image, polygon, arc] };
        using DocumentTabViewModel tab = new(document);

        tab.Designer.Select(text.Id, false);
        Assert.True(tab.Properties.IsTextElement);
        Assert.True(tab.Properties.SupportsFont);
        Assert.False(tab.Properties.IsImageElement);
        Assert.False(tab.Properties.SupportsPointEditing);

        tab.Designer.Select(image.Id, false);
        Assert.True(tab.Properties.IsImageElement);
        Assert.True(tab.Properties.SupportsStroke);
        Assert.True(tab.Properties.SupportsFill);
        Assert.False(tab.Properties.SupportsFont);

        tab.Designer.Select(polygon.Id, false);
        Assert.True(tab.Properties.SupportsPointEditing);
        Assert.True(tab.Properties.SupportsFill);
        Assert.False(tab.Properties.SupportsArcEditing);

        tab.Designer.Select(arc.Id, false);
        Assert.True(tab.Properties.SupportsArcEditing);
        Assert.False(tab.Properties.SupportsPointEditing);
        Assert.False(tab.Properties.SupportsFill);
    }

    [Fact]
    public async Task ImportedImageIsPackagedDisplayedAndUndoable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rmpp-image-{Guid.NewGuid():N}.png");
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(path, png);
        try
        {
            TemplateDocument document = TemplateDocument.CreateNew("图片上传");
            ImageElement image = new()
            {
                LayerId = document.Layers[0].Id,
                Bounds = new MmRect(10, 10, 30, 20),
            };
            document = document with { Elements = [image] };
            using DocumentTabViewModel tab = new(document);
            tab.Designer.Select(image.Id, false);

            await tab.Designer.ImportImageAsync(path);

            ImageElement imported = Assert.IsType<ImageElement>(Assert.Single(tab.Session.State.Document.Elements));
            Guid assetId = Assert.IsType<Guid>(imported.AssetId);
            Assert.Equal(Path.GetFileName(path), tab.Properties.ImageFileName);
            Assert.Equal(png, tab.Session.State.AssetContents[assetId].ToArray());
            Assert.Equal(png, tab.CreateRecoveryContent().Assets[assetId]);

            Assert.True(tab.Dispatcher.Undo());
            Assert.Null(Assert.IsType<ImageElement>(Assert.Single(tab.Session.State.Document.Elements)).AssetId);
            Assert.Empty(tab.Session.State.Document.Assets);

            Assert.True(tab.Dispatcher.Redo());
            Assert.Equal(assetId, Assert.IsType<ImageElement>(Assert.Single(tab.Session.State.Document.Elements)).AssetId);
            Assert.Equal(png, tab.CreateRecoveryContent().Assets[assetId]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedQrCenterIconIsPackagedAndUndoable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rmpp-qr-icon-{Guid.NewGuid():N}.png");
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(path, png);
        try
        {
            TemplateDocument document = TemplateDocument.CreateNew("二维码图标上传");
            BarcodeElement barcode = new()
            {
                LayerId = document.Layers[0].Id,
                Bounds = new MmRect(10, 10, 30, 30),
                Symbology = BarcodeSymbology.QrCode,
                Content = ElementExpression.Literal("RMPP QR ICON"),
            };
            document = document with { Elements = [barcode] };
            using DocumentTabViewModel tab = new(document);
            tab.Designer.Select(barcode.Id, false);

            await tab.Designer.ImportBarcodeCenterIconAsync(path);

            BarcodeElement imported = Assert.IsType<BarcodeElement>(Assert.Single(tab.Designer.Document.Elements));
            Guid assetId = Assert.IsType<Guid>(imported.CenterIconAssetId);
            Assert.Equal(3, imported.ErrorCorrectionLevel);
            Assert.Equal(Path.GetFileName(path), tab.Properties.BarcodeCenterIconFileName);
            Assert.Equal(png, tab.Session.State.AssetContents[assetId].ToArray());

            tab.Properties.BarcodeCenterIconScale = 0.22;
            Assert.Equal(0.22, Assert.IsType<BarcodeElement>(Assert.Single(tab.Designer.Document.Elements)).CenterIconScale);
            tab.Properties.BarcodeCenterIconScale = 0.5;
            Assert.Equal(0.22, Assert.IsType<BarcodeElement>(Assert.Single(tab.Designer.Document.Elements)).CenterIconScale);

            Assert.True(tab.Dispatcher.Undo());
            Assert.Equal(BarcodeElement.DefaultCenterIconScale,
                Assert.IsType<BarcodeElement>(Assert.Single(tab.Designer.Document.Elements)).CenterIconScale);
            Assert.True(tab.Dispatcher.Undo());
            Assert.Null(Assert.IsType<BarcodeElement>(Assert.Single(tab.Designer.Document.Elements)).CenterIconAssetId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImageCropCoordinatesCommitIndependentlyAndCanBeCleared()
    {
        TemplateDocument document = TemplateDocument.CreateNew("图片裁剪");
        ImageElement image = new()
        {
            LayerId = document.Layers[0].Id,
            Bounds = new MmRect(10, 10, 30, 20),
        };
        document = document with { Elements = [image] };
        using DocumentTabViewModel tab = new(document);
        tab.Designer.Select(image.Id, false);

        tab.Properties.ImageCropX = 1.25;
        tab.Properties.ImageCropY = 2.5;
        tab.Properties.ImageCropWidth = 12.75;
        tab.Properties.ImageCropHeight = 8.5;

        ImageElement cropped = Assert.IsType<ImageElement>(Assert.Single(tab.Designer.Document.Elements));
        Assert.Equal(new MmRect(1.25, 2.5, 12.75, 8.5), cropped.Crop);

        tab.Properties.ImageCropWidth = 0;
        Assert.Equal(12.75, Assert.IsType<ImageElement>(Assert.Single(tab.Designer.Document.Elements)).Crop?.Width);

        tab.Properties.ClearImageCrop();
        Assert.Null(Assert.IsType<ImageElement>(Assert.Single(tab.Designer.Document.Elements)).Crop);
    }

    [Fact]
    public void FontsAndRgbaStylesRoundTripIntoTheSharedRenderScene()
    {
        TemplateDocument document = TemplateDocument.CreateNew("字体颜色");
        Guid layerId = document.Layers[0].Id;
        TextElement text = new() { LayerId = layerId, Bounds = new MmRect(5, 5, 50, 15) };
        PolygonElement polygon = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(10, 30, 30, 20),
            Points = [new MmPoint(15, 0), new MmPoint(30, 20), new MmPoint(0, 20)],
        };
        document = document with { Elements = [text, polygon] };
        using DocumentTabViewModel tab = new(document);

        tab.Designer.Select(text.Id, false);
        string installedFont = Assert.Single(tab.Properties.FontFamilies.Take(1));
        tab.Properties.FontFamily = installedFont;
        tab.Properties.FontBold = true;
        tab.Properties.FontItalic = true;
        tab.Properties.TextColor = "#80402010";

        TextElement updatedText = Assert.IsType<TextElement>(tab.Designer.Document.Elements[0]);
        Assert.Equal(installedFont, updatedText.TextStyle.FontFamily);
        Assert.True(updatedText.TextStyle.IsBold);
        Assert.True(updatedText.TextStyle.IsItalic);
        Assert.Equal(new RgbaColor(0x40, 0x20, 0x10, 0x80), updatedText.TextStyle.Color);

        tab.Designer.Select(polygon.Id, false);
        tab.Properties.Opacity = 0.5;
        tab.Properties.StrokeWidth = 0.6;
        tab.Properties.StrokeColor = "#7F102030";
        tab.Properties.FillMode = "Hatch";
        tab.Properties.HatchForegroundColor = "#80FF0000";
        tab.Properties.HatchBackgroundColor = "#40FFFF00";

        PolygonElement updatedPolygon = Assert.IsType<PolygonElement>(tab.Designer.Document.Elements[1]);
        HatchFill hatch = Assert.IsType<HatchFill>(updatedPolygon.Fill);
        Assert.Equal(new RgbaColor(255, 0, 0, 0x80), hatch.Foreground);
        Assert.Equal(new RgbaColor(255, 255, 0, 0x40), hatch.Background);
        Assert.Equal(new RgbaColor(0x10, 0x20, 0x30, 0x7F), updatedPolygon.Stroke.Color);

        RenderPathCommand command = Assert.IsType<RenderPathCommand>(
            new RenderSceneBuilder().Build(tab.Designer.Document).Pages[0].Commands.Single(item => item.SourceId == polygon.Id));
        RenderHatchFill renderedHatch = Assert.IsType<RenderHatchFill>(command.Fill);
        Assert.Equal(hatch.Foreground, renderedHatch.Foreground);
        Assert.Equal(hatch.Background, renderedHatch.Background);
        Assert.Equal(0.5, command.Opacity);
    }
}
