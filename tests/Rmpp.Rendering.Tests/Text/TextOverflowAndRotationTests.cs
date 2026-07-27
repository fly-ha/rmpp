using SkiaSharp;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Rmpp.Rendering.Text;
using Xunit;

namespace Rmpp.Rendering.Tests.Text;

public sealed class TextOverflowAndRotationTests
{
    [Fact]
    public void WarnModeReportsOverflowWithoutChangingRequestedFontSize()
    {
        LocalFontCatalog catalog = new();
        TextLayoutResult result = new TextLayoutService(catalog).Layout(new TextLayoutRequest
        {
            Text = "This text cannot fit",
            Bounds = new MmRect(0, 0, 5, 3),
            Style = Style(catalog.Families[0]) with
            {
                FontSizePoints = 18,
                Wrap = false,
                OverflowMode = TextOverflowMode.Warn,
            },
        });

        Assert.True(result.Overflowed);
        Assert.Equal(18, result.ActualFontSizePoints);
        Assert.Contains(result.Issues, static issue => issue.Code == "text-overflow");
    }

    [Fact]
    public void RotatedGlyphRunsRenderThroughCommandTransform()
    {
        LocalFontCatalog catalog = new();
        RenderTextStyle style = Style(catalog.Families[0]);
        TextLayoutResult layout = new TextLayoutService(catalog).Layout(new TextLayoutRequest
        {
            Text = "RMPP",
            Bounds = new MmRect(0, 0, 30, 10),
            Style = style,
        });
        MmRect elementBounds = new(20, 10, 30, 10);
        RenderTextCommand command = new()
        {
            SourceId = Guid.NewGuid(),
            Transform = RenderTransform.ForElement(elementBounds, new Angle(30)),
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, 30, 10)),
            Text = "RMPP",
            LocalBounds = new MmRect(0, 0, 30, 10),
            Style = style,
            GlyphRuns = layout.GlyphRuns,
        };
        MmSize pageSize = new(70, 40);
        RenderPage page = new()
        {
            PageNumber = 1,
            Size = pageSize,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, pageSize.Width, pageSize.Height)),
            Commands = [command],
        };

        using SKBitmap bitmap = new SkiaBitmapRenderer().Render(page, 150);

        Assert.Contains(Enumerable.Range(0, bitmap.Height), y => Enumerable.Range(0, bitmap.Width)
            .Any(x => bitmap.GetPixel(x, y).Red < 128));
    }

    private static RenderTextStyle Style(string family) => new()
    {
        FontFamily = family,
        FontSizePoints = 12,
        Color = RgbaColor.Black,
        Wrap = true,
        LineSpacing = 1,
    };
}
