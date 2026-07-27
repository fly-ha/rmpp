using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Infrastructure.Tests.Templates;

internal static class TemplatePackageTestData
{
    public static TemplatePackageContent Create()
    {
        byte[] assetBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        Guid assetId = Guid.NewGuid();
        AssetReference asset = new(assetId, "logo.png", "image/png", AssetHashService.ComputeSha256(assetBytes));
        TemplateDocument source = TemplateDocument.CreateNew("Package test");
        Guid layerId = source.Layers[0].Id;
        return new TemplatePackageContent
        {
            Document = source with
            {
                Assets = [asset],
                Elements =
                [
                    new TextElement
                    {
                        Name = "Title",
                        LayerId = layerId,
                        Bounds = new MmRect(10, 10, 50, 12),
                    },
                    new PolygonElement
                    {
                        Name = "Hatched polygon",
                        LayerId = layerId,
                        Bounds = new MmRect(20, 30, 40, 40),
                        Points = [new MmPoint(0, 0), new MmPoint(40, 0), new MmPoint(20, 40)],
                        Fill = new HatchFill { Pattern = HatchPattern.DiagonalCross, SpacingMm = 1.5 },
                    },
                    new ImageElement
                    {
                        Name = "Logo",
                        LayerId = layerId,
                        Bounds = new MmRect(70, 10, 20, 20),
                        AssetId = assetId,
                    },
                ],
            },
            Assets = new Dictionary<Guid, byte[]> { [assetId] = assetBytes },
            PreviewPng = assetBytes,
        };
    }
}
