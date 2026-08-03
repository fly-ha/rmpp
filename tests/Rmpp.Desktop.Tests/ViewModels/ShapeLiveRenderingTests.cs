using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Desktop.Tests.ViewModels;

public sealed class ShapeLiveRenderingTests
{
    [Fact]
    public void ShapePropertyChangesImmediatelyEnterTheSharedRenderScene()
    {
        TemplateDocument document = TemplateDocument.CreateNew("图形实时渲染");
        Guid layerId = document.Layers[0].Id;
        RectangleElement rectangle = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(10, 10, 40, 25),
        };
        LineElement line = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(10, 45, 30, 10),
            Start = new MmPoint(0, 0),
            End = new MmPoint(30, 10),
        };
        ArcElement arc = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(60, 10, 30, 30),
        };
        document = document with { Elements = [rectangle, line, arc] };
        using DocumentTabViewModel tab = new(document);

        tab.Designer.Select(rectangle.Id, false);
        tab.Properties.CornerRadius = 4;
        tab.Properties.StrokeWidth = 0.8;
        tab.Properties.FillMode = "Hatch";
        tab.Properties.HatchPattern = HatchPattern.Grid;

        RectangleElement updatedRectangle = Assert.IsType<RectangleElement>(tab.Session.State.Document.Elements[0]);
        Assert.Equal(4, updatedRectangle.CornerRadius.Width);
        Assert.Equal(0.8, updatedRectangle.Stroke.WidthMm);
        Assert.Equal(HatchPattern.Grid, Assert.IsType<HatchFill>(updatedRectangle.Fill).Pattern);
        RenderPathCommand rectangleCommand = Assert.IsType<RenderPathCommand>(
            new RenderSceneBuilder().Build(tab.Session.State.Document).Pages[0].Commands.Single(command => command.SourceId == rectangle.Id));
        Assert.Equal(0.8, rectangleCommand.Stroke.WidthMm);
        Assert.IsType<RenderHatchFill>(rectangleCommand.Fill);

        tab.Designer.Select(line.Id, false);
        tab.Properties.Points = "0,0; 25,5";
        LineElement updatedLine = Assert.IsType<LineElement>(tab.Session.State.Document.Elements[1]);
        Assert.Equal(new MmPoint(25, 5), updatedLine.End);
        Assert.IsType<RenderPathCommand>(
            new RenderSceneBuilder().Build(tab.Session.State.Document).Pages[0].Commands.Single(command => command.SourceId == line.Id));

        tab.Designer.Select(arc.Id, false);
        tab.Properties.StartAngle = 30;
        tab.Properties.SweepDegrees = 210;
        ArcElement updatedArc = Assert.IsType<ArcElement>(tab.Session.State.Document.Elements[2]);
        Assert.Equal(30, updatedArc.StartAngle.Degrees);
        Assert.Equal(210, updatedArc.SweepDegrees);
        Assert.IsType<RenderPathCommand>(
            new RenderSceneBuilder().Build(tab.Session.State.Document).Pages[0].Commands.Single(command => command.SourceId == arc.Id));
    }
}
