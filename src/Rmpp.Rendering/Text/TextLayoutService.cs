using System.Globalization;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Text;

/// <summary>使用本机 Skia/HarfBuzz 在毫米空间内完成文本塑形、换行、对齐和溢出判断。</summary>
public sealed class TextLayoutService(LocalFontCatalog? fontCatalog = null)
{
    private const double PointsToMillimetres = 25.4 / 72;
    private readonly LocalFontCatalog fontCatalog = fontCatalog ?? new LocalFontCatalog();

    public TextLayoutResult Layout(TextLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Style);
        if (request.Bounds.Width <= 0 || request.Bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Text bounds must have positive width and height.");
        }

        FontResolution resolution = fontCatalog.Resolve(request.Style.FontFamily);
        double minimum = Math.Clamp(request.MinimumAutoFitPoints, 1, request.Style.FontSizePoints);
        double fontSize = request.Style.FontSizePoints;
        LayoutPass pass = LayoutAtSize(request, resolution, fontSize);
        if (request.Style.OverflowMode == TextOverflowMode.AutoFit)
        {
            while (pass.Overflowed && fontSize - 0.5 >= minimum)
            {
                fontSize -= 0.5;
                pass = LayoutAtSize(request, resolution, fontSize);
            }
        }

        List<TextLayoutIssue> issues = [];
        if (resolution.UsedFallback)
        {
            issues.Add(new TextLayoutIssue(
                "missing-font",
                $"Font '{resolution.RequestedFamily}' is not installed; using '{resolution.ResolvedFamily}'.",
                TextLayoutIssueSeverity.Warning));
        }

        if (pass.MissingGlyphCount > 0)
        {
            issues.Add(new TextLayoutIssue(
                "missing-glyph",
                $"Font '{resolution.ResolvedFamily}' cannot display {pass.MissingGlyphCount} text element(s).",
                TextLayoutIssueSeverity.Warning));
        }

        if (pass.Overflowed)
        {
            issues.Add(new TextLayoutIssue(
                "text-overflow",
                "Text exceeds the available physical bounds.",
                TextLayoutIssueSeverity.Warning));
        }
        else if (request.Style.OverflowMode == TextOverflowMode.ExpandHeight && pass.RequiredSize.Height > request.Bounds.Height)
        {
            issues.Add(new TextLayoutIssue(
                "text-expand-height",
                $"Text requires {pass.RequiredSize.Height:0.###} mm height.",
                TextLayoutIssueSeverity.Information));
        }

        return new TextLayoutResult
        {
            Font = resolution,
            GlyphRuns = pass.Runs,
            RequiredSize = pass.RequiredSize,
            ActualFontSizePoints = fontSize,
            Overflowed = pass.Overflowed,
            Issues = issues,
        };
    }

    private LayoutPass LayoutAtSize(TextLayoutRequest request, FontResolution resolution, double fontSizePoints)
    {
        float fontSizeMm = checked((float)(fontSizePoints * PointsToMillimetres));
        using SKTypeface typeface = fontCatalog.OpenTypeface(resolution, request.Style.IsBold, request.Style.IsItalic);
        using SKFont font = new(typeface, fontSizeMm) { LinearMetrics = true, Subpixel = true };
        using SKShaper shaper = new(typeface);
        SKFontMetrics metrics = font.Metrics;
        double naturalLineHeight = Math.Max(font.Spacing, metrics.Descent - metrics.Ascent);
        double lineHeight = naturalLineHeight * request.Style.LineSpacing;
        List<ShapedLine> shapedLines = WrapLines(request.Text, request.Style.Wrap, request.Bounds.Width, request.Style.LetterSpacingMm, shaper, font);
        if (shapedLines.Count == 0)
        {
            shapedLines.Add(ShapeLine(string.Empty, request.Style.LetterSpacingMm, shaper, font));
        }

        double requiredWidth = shapedLines.Max(static line => line.WidthMm);
        double requiredHeight = shapedLines.Count * lineHeight;
        bool widthOverflow = requiredWidth > request.Bounds.Width + 0.0001;
        bool heightOverflow = requiredHeight > request.Bounds.Height + 0.0001;
        bool overflowed = widthOverflow || request.Style.OverflowMode != TextOverflowMode.ExpandHeight && heightOverflow;
        double blockHeight = request.Style.OverflowMode == TextOverflowMode.ExpandHeight
            ? requiredHeight
            : Math.Min(requiredHeight, request.Bounds.Height);
        double top = request.Style.VerticalAlignment switch
        {
            TextVerticalAlignment.Center => Math.Max(0, (request.Bounds.Height - blockHeight) / 2),
            TextVerticalAlignment.Bottom => Math.Max(0, request.Bounds.Height - blockHeight),
            _ => 0,
        };

        List<RenderGlyphRun> runs = [];
        int missingGlyphs = 0;
        for (int lineIndex = 0; lineIndex < shapedLines.Count; lineIndex++)
        {
            ShapedLine line = shapedLines[lineIndex];
            double x = request.Style.HorizontalAlignment switch
            {
                TextHorizontalAlignment.Center => Math.Max(0, (request.Bounds.Width - line.WidthMm) / 2),
                TextHorizontalAlignment.Right => Math.Max(0, request.Bounds.Width - line.WidthMm),
                _ => 0,
            };
            double extraGap = request.Style.HorizontalAlignment == TextHorizontalAlignment.Justify
                && lineIndex < shapedLines.Count - 1
                && line.Glyphs.Count > 1
                ? Math.Max(0, request.Bounds.Width - line.WidthMm) / (line.Glyphs.Count - 1)
                : 0;
            double baseline = top + -metrics.Ascent + lineIndex * lineHeight;
            RenderGlyph[] glyphs = new RenderGlyph[line.Glyphs.Count];
            for (int glyphIndex = 0; glyphIndex < glyphs.Length; glyphIndex++)
            {
                ShapedGlyph glyph = line.Glyphs[glyphIndex];
                double justifiedOffset = glyphIndex * extraGap;
                glyphs[glyphIndex] = new RenderGlyph(
                    checked((int)glyph.GlyphId),
                    new MmPoint(x + glyph.XMm + justifiedOffset, baseline + glyph.YMm),
                    glyph.AdvanceMm + (glyphIndex < glyphs.Length - 1 ? extraGap : 0));
                if (glyph.GlyphId == 0)
                {
                    missingGlyphs++;
                }
            }

            double runWidth = line.WidthMm + Math.Max(0, glyphs.Length - 1) * extraGap;
            runs.Add(new RenderGlyphRun(
                resolution.ResolvedFamily,
                fontSizePoints,
                request.Style.Color,
                glyphs,
                new MmRect(x, top + lineIndex * lineHeight, runWidth, lineHeight)));
        }

        return new LayoutPass(
            runs,
            new MmSize(requiredWidth, requiredHeight),
            overflowed,
            missingGlyphs);
    }

    /// <summary>按显式换行和字素簇贪心换行，避免拆开代理项或组合字符。</summary>
    private static List<ShapedLine> WrapLines(
        string text,
        bool wrap,
        double maximumWidthMm,
        double letterSpacingMm,
        SKShaper shaper,
        SKFont font)
    {
        string normalized = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        List<ShapedLine> lines = [];
        foreach (string paragraph in normalized.Split('\n'))
        {
            if (!wrap || paragraph.Length == 0)
            {
                lines.Add(ShapeLine(paragraph, letterSpacingMm, shaper, font));
                continue;
            }

            List<string> elements = TextElements(paragraph);
            string current = string.Empty;
            ShapedLine currentShape = ShapeLine(current, letterSpacingMm, shaper, font);
            foreach (string element in elements)
            {
                string candidate = current + element;
                ShapedLine candidateShape = ShapeLine(candidate, letterSpacingMm, shaper, font);
                if (current.Length > 0 && candidateShape.WidthMm > maximumWidthMm)
                {
                    lines.Add(currentShape);
                    current = element;
                    currentShape = ShapeLine(current, letterSpacingMm, shaper, font);
                }
                else
                {
                    current = candidate;
                    currentShape = candidateShape;
                }
            }

            lines.Add(currentShape);
        }

        return lines;
    }

    private static List<string> TextElements(string value)
    {
        List<string> elements = [];
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    private static ShapedLine ShapeLine(string value, double letterSpacingMm, SKShaper shaper, SKFont font)
    {
        SKShaper.Result result = shaper.Shape(value, font);
        float[] widths = result.Codepoints.Length == 0
            ? Array.Empty<float>()
            : font.GetGlyphWidths(result.Codepoints.Select(static codepoint => checked((ushort)codepoint)).ToArray());
        ShapedGlyph[] glyphs = new ShapedGlyph[result.Codepoints.Length];
        for (int index = 0; index < glyphs.Length; index++)
        {
            double advance = index + 1 < result.Points.Length
                ? result.Points[index + 1].X - result.Points[index].X
                : result.Width - result.Points[index].X;
            if (advance <= 0 && index < widths.Length)
            {
                advance = widths[index];
            }

            double spacingOffset = index * letterSpacingMm;
            glyphs[index] = new ShapedGlyph(
                result.Codepoints[index],
                result.Points[index].X + spacingOffset,
                result.Points[index].Y,
                Math.Max(0, advance) + (index < glyphs.Length - 1 ? letterSpacingMm : 0));
        }

        double width = result.Width + Math.Max(0, glyphs.Length - 1) * letterSpacingMm;
        return new ShapedLine(glyphs, Math.Max(0, width));
    }

    private sealed record ShapedGlyph(uint GlyphId, double XMm, double YMm, double AdvanceMm);
    private sealed record ShapedLine(IReadOnlyList<ShapedGlyph> Glyphs, double WidthMm);
    private sealed record LayoutPass(IReadOnlyList<RenderGlyphRun> Runs, MmSize RequiredSize, bool Overflowed, int MissingGlyphCount);
}
