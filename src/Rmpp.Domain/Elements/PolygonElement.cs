namespace Rmpp.Domain.Elements;

public sealed record PolygonElement : PathElement
{
    public override TemplateElement Validate()
    {
        base.Validate();
        if (Points.Distinct().Count() < 3)
        {
            throw new InvalidOperationException("A polygon requires at least three distinct points.");
        }

        return this;
    }
}
