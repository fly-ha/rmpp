using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class EditorCommandTests
{
    [Fact]
    public void MoveSkipsLockedElementsAndResizeScalesPathPoints()
    {
        RectangleElement locked = TestDocumentFactory.Rectangle() with { IsLocked = true };
        PolygonElement polygon = new()
        {
            Bounds = new MmRect(10, 20, 20, 10),
            Points = [new MmPoint(0, 0), new MmPoint(20, 0), new MmPoint(10, 10)],
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(locked, polygon);

        TemplateDocument moved = new MoveElementsCommand([locked.Id, polygon.Id], 5, 2).Execute(document);
        TemplateDocument resized = new ResizeElementsCommand(new Dictionary<Guid, MmRect>
        {
            [polygon.Id] = new MmRect(15, 22, 40, 20),
        }).Execute(moved);

        Assert.Equal(10, moved.Elements[0].Bounds.X);
        PolygonElement result = Assert.IsType<PolygonElement>(resized.Elements[1]);
        Assert.Equal(new MmPoint(40, 0), result.Points[1]);
        Assert.Equal(new MmPoint(20, 20), result.Points[2]);
    }

    [Fact]
    public void PointRotationLayerAndStateCommandsPreserveIdentity()
    {
        PolylineElement line = new()
        {
            Bounds = new MmRect(0, 0, 20, 10),
            Points = [new MmPoint(0, 0), new MmPoint(20, 10)],
        };
        (TemplateDocument source, LayerDefinition firstLayer) = TestDocumentFactory.Create(line);
        LayerDefinition secondLayer = new(Guid.NewGuid(), "Second");
        TemplateDocument document = source with { Layers = [firstLayer, secondLayer] };

        document = new EditPointsCommand(line.Id, [new MmPoint(0, 10), new MmPoint(20, 0)]).Execute(document);
        document = new RotateElementsCommand(new Dictionary<Guid, Angle> { [line.Id] = new Angle(45) }).Execute(document);
        document = new ChangeElementLayerCommand([line.Id], secondLayer.Id).Execute(document);
        document = new SetElementStateCommand([line.Id], isLocked: true, isVisible: false, isPrintable: false).Execute(document);

        PolylineElement result = Assert.IsType<PolylineElement>(Assert.Single(document.Elements));
        Assert.Equal(line.Id, result.Id);
        Assert.Equal(new Angle(45), result.Rotation);
        Assert.Equal(secondLayer.Id, result.LayerId);
        Assert.True(result.IsLocked);
        Assert.False(result.IsVisible);
        Assert.False(result.IsPrintable);
    }

    [Fact]
    public void ReorderAndBackgroundCommandsAreDeterministic()
    {
        RectangleElement first = TestDocumentFactory.Rectangle(zIndex: 0);
        RectangleElement second = TestDocumentFactory.Rectangle(zIndex: 1);
        RectangleElement third = TestDocumentFactory.Rectangle(zIndex: 2);
        (TemplateDocument document, _) = TestDocumentFactory.Create(first, second, third);
        AssetReference asset = new(Guid.NewGuid(), "form.png", "image/png", new string('a', 64));
        BackgroundDefinition background = new()
        {
            AssetId = asset.Id,
            Bounds = new MmRect(0, 0, 210, 297),
        };

        document = new ReorderElementsCommand([first.Id], ElementOrderPlacement.BringToFront).Execute(document);
        document = new AddBackgroundCommand(background).Execute(document with { Assets = [asset] });
        document = new ChangeBackgroundPropertiesCommand(
            background.Id,
            value => value with { IsPrintable = true }).Execute(document);

        Assert.Equal([second.Id, third.Id, first.Id], document.Elements.Select(static element => element.Id));
        Assert.True(Assert.Single(document.Backgrounds).IsPrintable);
        Assert.Empty(new DeleteBackgroundsCommand([background.Id]).Execute(document).Backgrounds);
    }
}
