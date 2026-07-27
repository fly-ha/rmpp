using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Text;

public enum TextLayoutIssueSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record TextLayoutIssue(string Code, string Message, TextLayoutIssueSeverity Severity);

/// <summary>保存已塑形字形、实际字号、所需空间和可供 UI 展示的排版问题。</summary>
public sealed record TextLayoutResult
{
    public required FontResolution Font { get; init; }
    public required IReadOnlyList<RenderGlyphRun> GlyphRuns { get; init; }
    public required MmSize RequiredSize { get; init; }
    public double ActualFontSizePoints { get; init; }
    public bool Overflowed { get; init; }
    public IReadOnlyList<TextLayoutIssue> Issues { get; init; } = Array.Empty<TextLayoutIssue>();
}
