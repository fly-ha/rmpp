namespace Rmpp.Domain.Elements;

public sealed record PolylineElement : PathElement
{
    public override TemplateElement Validate()
    {
        base.Validate();
        if (Points.Count < 2)
        {
            throw new InvalidOperationException("A polyline requires at least two points.");
        }

        return this;
    }
}
