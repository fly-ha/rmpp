using Rmpp.Domain.Common;

namespace Rmpp.Domain.Documents;

public sealed record LayerDefinition
{
    public LayerDefinition(Guid id, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Layer id cannot be empty.", nameof(id)) : id;
        Name = DomainGuard.Required(name, nameof(name));
    }

    public Guid Id { get; }
    public string Name { get; init; }
    public bool IsVisible { get; init; } = true;
    public bool IsPrintable { get; init; } = true;
    public bool IsLocked { get; init; }
}
