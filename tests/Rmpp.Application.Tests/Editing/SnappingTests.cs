using Rmpp.Application.Editing.Snapping;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class SnappingTests
{
    [Fact]
    public void GuideSnapUsesZoomAwarePixelTolerance()
    {
        GuideDefinition guide = new(Guid.NewGuid(), GuideOrientation.Vertical, 50);
        SnapContext context = new(
            new MmSize(210, 297),
            [guide],
            Array.Empty<TemplateElement>(),
            new HashSet<Guid>());

        SnapResult result = SnapEngine.Snap(
            new MmRect(10, 10, 20, 10),
            new MmPoint(19.2, 0),
            context,
            new SnapOptions
            {
                SnapToGrid = false,
                SnapToPage = false,
                SnapToElements = false,
                TolerancePixels = 4,
                PixelsPerMillimetre = 4,
            });

        Assert.True(result.Snapped);
        Assert.Equal(20, result.AdjustedDelta.X, 6);
        Assert.Equal(SnapCandidateKind.Guide, result.HorizontalMatch?.Candidate.Kind);
    }

    [Fact]
    public void TemporaryOverrideLeavesDesiredDeltaUntouched()
    {
        SnapResult result = SnapEngine.Snap(
            new MmRect(0, 0, 10, 10),
            new MmPoint(0.3, 0.4),
            new SnapContext(new MmSize(100, 100), [], [], new HashSet<Guid>()),
            new SnapOptions { IsTemporarilyDisabled = true });

        Assert.Equal(new MmPoint(0.3, 0.4), result.AdjustedDelta);
        Assert.False(result.Snapped);
    }

    [Fact]
    public void AlignmentAndDistributionReturnExactBounds()
    {
        RectangleElement first = TestDocumentFactory.Rectangle(x: 10, width: 10);
        RectangleElement second = TestDocumentFactory.Rectangle(x: 40, width: 20);
        RectangleElement third = TestDocumentFactory.Rectangle(x: 90, width: 10);
        (TemplateDocument document, _) = TestDocumentFactory.Create(first, second, third);

        IReadOnlyDictionary<Guid, MmRect> aligned = AlignmentService.Calculate(
            document,
            [first.Id, second.Id, third.Id],
            AlignmentMode.Left);
        IReadOnlyDictionary<Guid, MmRect> distributed = DistributionService.Calculate(
            document,
            [first.Id, second.Id, third.Id],
            DistributionAxis.Horizontal);

        Assert.All(aligned.Values, bounds => Assert.Equal(10, bounds.X));
        Assert.Equal(45, distributed[second.Id].X, 6);
        Assert.Equal(90, distributed[third.Id].X, 6);
    }
}
