using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Text;
using Xunit;

namespace Rmpp.Rendering.Tests.Text;

public sealed class TextLayoutServiceTests
{
    [Fact]
    public void MultilingualTextProducesPositionedGlyphRuns()
    {
        LocalFontCatalog catalog = new();
        TextLayoutResult result = new TextLayoutService(catalog).Layout(new TextLayoutRequest
        {
            Text = "红枫 RMPP Studio",
            Bounds = new MmRect(0, 0, 60, 20),
            Style = Style(catalog.Families[0]) with { HorizontalAlignment = TextHorizontalAlignment.Center },
        });

        Assert.NotEmpty(result.GlyphRuns);
        Assert.NotEmpty(result.GlyphRuns.SelectMany(static run => run.Glyphs));
        Assert.All(result.GlyphRuns.SelectMany(static run => run.Glyphs), static glyph => Assert.True(glyph.Origin.X >= 0));
    }

    [Fact]
    public void WrappingAndVerticalAlignmentStayInsidePhysicalBounds()
    {
        LocalFontCatalog catalog = new();
        TextLayoutResult result = new TextLayoutService(catalog).Layout(new TextLayoutRequest
        {
            Text = "红枫叶定位打印所见即所得",
            Bounds = new MmRect(0, 0, 12, 30),
            Style = Style(catalog.Families[0]) with
            {
                FontSizePoints = 10,
                VerticalAlignment = TextVerticalAlignment.Bottom,
                Wrap = true,
            },
        });

        Assert.True(result.GlyphRuns.Count > 1);
        Assert.All(result.GlyphRuns, static run => Assert.True(run.Bounds.Bottom <= 30.001));
    }

    [Fact]
    public void AutoFitReducesFontUntilTextFits()
    {
        LocalFontCatalog catalog = new();
        TextLayoutResult result = new TextLayoutService(catalog).Layout(new TextLayoutRequest
        {
            Text = "RMPP",
            Bounds = new MmRect(0, 0, 16, 5),
            Style = Style(catalog.Families[0]) with
            {
                FontSizePoints = 20,
                Wrap = false,
                OverflowMode = TextOverflowMode.AutoFit,
            },
        });

        Assert.True(result.ActualFontSizePoints < 20);
        Assert.False(result.Overflowed);
    }

    [Fact]
    public void MissingFontIsReportedWithResolvedFallback()
    {
        TextLayoutResult result = new TextLayoutService().Layout(new TextLayoutRequest
        {
            Text = "offline",
            Bounds = new MmRect(0, 0, 30, 10),
            Style = Style("__RMPP_MISSING_FONT__"),
        });

        Assert.True(result.Font.UsedFallback);
        Assert.Contains(result.Issues, static issue => issue.Code == "missing-font");
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
