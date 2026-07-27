using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Tests;

internal static class TestDocumentFactory
{
    public static (TemplateDocument Document, LayerDefinition Layer) Create(
        params TemplateElement[] elements)
    {
        LayerDefinition layer = new(Guid.NewGuid(), "Default");
        TemplateDocument source = TemplateDocument.CreateNew();
        TemplateElement[] assigned = elements.Select(element =>
            element.LayerId == Guid.Empty ? element with { LayerId = layer.Id } : element).ToArray();
        return (source with { Layers = [layer], Elements = assigned }, layer);
    }

    public static RectangleElement Rectangle(
        double x = 10,
        double y = 10,
        double width = 20,
        double height = 10,
        int zIndex = 0) =>
        new()
        {
            Name = "Rectangle",
            Bounds = new MmRect(x, y, width, height),
            ZIndex = zIndex,
        };
}
