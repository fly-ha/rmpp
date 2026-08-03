using System.IO;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
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
}
