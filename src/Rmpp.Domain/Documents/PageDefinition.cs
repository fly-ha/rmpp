using Rmpp.Domain.Layout;

namespace Rmpp.Domain.Documents;

public sealed record PageDefinition
{
    public required MediaDefinition Media { get; init; }
    public DocumentLayout Layout { get; init; } = new SinglePageLayout();
}
