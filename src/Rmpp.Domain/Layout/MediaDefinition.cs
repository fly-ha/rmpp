using Rmpp.Domain.Common;
using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Layout;

public enum PageOrientation { Portrait, Landscape }

public sealed record MediaDefinition
{
    public MediaDefinition(string name, MmSize size, PageOrientation orientation = PageOrientation.Portrait)
    {
        Name = DomainGuard.Required(name, nameof(name));
        if (size.IsEmpty)
        {
            throw new ArgumentException("Media size must be positive.", nameof(size));
        }

        Size = size;
        Orientation = orientation;
    }

    public string Name { get; }
    public MmSize Size { get; }
    public PageOrientation Orientation { get; }
}
