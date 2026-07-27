using Rmpp.Domain.Common;
using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Elements;

public abstract record TemplateElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Element";
    public Guid LayerId { get; init; }
    public int ZIndex { get; init; }
    public MmRect Bounds { get; init; }
    public Angle Rotation { get; init; } = Angle.Zero;
    public double Opacity { get; init; } = 1;
    public bool IsVisible { get; init; } = true;
    public bool IsPrintable { get; init; } = true;
    public bool IsLocked { get; init; }

    public virtual TemplateElement Validate()
    {
        DomainGuard.Required(Name, nameof(Name));
        DomainGuard.InRange(Opacity, 0, 1, nameof(Opacity));
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("Element id cannot be empty.");
        }

        return this;
    }
}
