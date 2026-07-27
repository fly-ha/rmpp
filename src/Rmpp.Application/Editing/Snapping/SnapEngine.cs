using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Snapping;

/// <summary>以缩放感知的屏幕容差计算网格、参考线、页面和元素吸附。</summary>
public sealed class SnapEngine
{
    public static SnapResult Snap(
        MmRect originalBounds,
        MmPoint desiredDelta,
        SnapContext context,
        SnapOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        SnapOptions effective = options ?? new SnapOptions();
        if (!effective.IsEnabled || effective.IsTemporarilyDisabled)
        {
            return new SnapResult(desiredDelta, null, null);
        }

        if (effective.PixelsPerMillimetre <= 0 || effective.TolerancePixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Snap scale and tolerance are invalid.");
        }

        double toleranceMm = effective.TolerancePixels / effective.PixelsPerMillimetre;
        MmRect desired = originalBounds.Translate(desiredDelta.X, desiredDelta.Y);
        List<SnapCandidate> candidates = BuildCandidates(context, effective);
        SnapMatch? horizontal = FindBest(
            [desired.X, desired.X + desired.Width / 2, desired.Right],
            SnapAxis.Vertical,
            candidates,
            effective,
            toleranceMm);
        SnapMatch? vertical = FindBest(
            [desired.Y, desired.Y + desired.Height / 2, desired.Bottom],
            SnapAxis.Horizontal,
            candidates,
            effective,
            toleranceMm);
        return new SnapResult(
            new MmPoint(
                desiredDelta.X + (horizontal?.AdjustmentMm ?? 0),
                desiredDelta.Y + (vertical?.AdjustmentMm ?? 0)),
            horizontal,
            vertical);
    }

    private static List<SnapCandidate> BuildCandidates(SnapContext context, SnapOptions options)
    {
        List<SnapCandidate> candidates = [];
        if (options.SnapToPage)
        {
            candidates.AddRange(
            [
                new SnapCandidate(SnapAxis.Vertical, 0, SnapCandidateKind.PageEdge, Label: "page-left"),
                new SnapCandidate(SnapAxis.Vertical, context.PageSize.Width / 2, SnapCandidateKind.PageCenter, Label: "page-centre-x"),
                new SnapCandidate(SnapAxis.Vertical, context.PageSize.Width, SnapCandidateKind.PageEdge, Label: "page-right"),
                new SnapCandidate(SnapAxis.Horizontal, 0, SnapCandidateKind.PageEdge, Label: "page-top"),
                new SnapCandidate(SnapAxis.Horizontal, context.PageSize.Height / 2, SnapCandidateKind.PageCenter, Label: "page-centre-y"),
                new SnapCandidate(SnapAxis.Horizontal, context.PageSize.Height, SnapCandidateKind.PageEdge, Label: "page-bottom"),
            ]);
        }

        if (options.SnapToGuides)
        {
            candidates.AddRange(context.Guides.Select(guide => new SnapCandidate(
                guide.Orientation == GuideOrientation.Vertical ? SnapAxis.Vertical : SnapAxis.Horizontal,
                guide.PositionMm,
                SnapCandidateKind.Guide,
                guide.Id,
                "guide")));
        }

        if (options.SnapToElements)
        {
            foreach (TemplateElement element in context.Elements.Where(element =>
                         element.IsVisible && !context.MovingElementIds.Contains(element.Id)))
            {
                candidates.AddRange(
                [
                    new SnapCandidate(SnapAxis.Vertical, element.Bounds.X, SnapCandidateKind.ElementEdge, element.Id, "element-left"),
                    new SnapCandidate(SnapAxis.Vertical, element.Bounds.X + element.Bounds.Width / 2, SnapCandidateKind.ElementCenter, element.Id, "element-centre-x"),
                    new SnapCandidate(SnapAxis.Vertical, element.Bounds.Right, SnapCandidateKind.ElementEdge, element.Id, "element-right"),
                    new SnapCandidate(SnapAxis.Horizontal, element.Bounds.Y, SnapCandidateKind.ElementEdge, element.Id, "element-top"),
                    new SnapCandidate(SnapAxis.Horizontal, element.Bounds.Y + element.Bounds.Height / 2, SnapCandidateKind.ElementCenter, element.Id, "element-centre-y"),
                    new SnapCandidate(SnapAxis.Horizontal, element.Bounds.Bottom, SnapCandidateKind.ElementEdge, element.Id, "element-bottom"),
                ]);
            }
        }

        return candidates;
    }

    private static SnapMatch? FindBest(
        IReadOnlyList<double> sourceAnchors,
        SnapAxis targetAxis,
        IReadOnlyList<SnapCandidate> candidates,
        SnapOptions options,
        double toleranceMm)
    {
        SnapMatch? best = null;
        foreach (double source in sourceAnchors)
        {
            if (options.SnapToGrid)
            {
                if (options.GridSpacingMm <= 0 || !double.IsFinite(options.GridSpacingMm))
                {
                    throw new ArgumentOutOfRangeException(nameof(options), "Grid spacing must be positive and finite.");
                }

                double target = Math.Round(source / options.GridSpacingMm, MidpointRounding.AwayFromZero) * options.GridSpacingMm;
                best = SelectCloser(best, new SnapMatch(
                    source,
                    target - source,
                    new SnapCandidate(targetAxis, target, SnapCandidateKind.Grid, Label: "grid")), toleranceMm);
            }

            foreach (SnapCandidate candidate in candidates.Where(candidate => candidate.Axis == targetAxis))
            {
                best = SelectCloser(
                    best,
                    new SnapMatch(source, candidate.PositionMm - source, candidate),
                    toleranceMm);
            }
        }

        return best;
    }

    private static SnapMatch? SelectCloser(SnapMatch? current, SnapMatch candidate, double toleranceMm)
    {
        double distance = Math.Abs(candidate.AdjustmentMm);
        if (distance > toleranceMm)
        {
            return current;
        }

        return current is null || distance < Math.Abs(current.AdjustmentMm)
            ? candidate
            : current;
    }
}
