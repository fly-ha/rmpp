using Rmpp.Domain.Data;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Printing;

namespace Rmpp.Domain.Documents;

public sealed record TemplateDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DocumentMetadata Metadata { get; init; } = new();
    public required PageDefinition Page { get; init; }
    public IReadOnlyList<LayerDefinition> Layers { get; init; } = Array.Empty<LayerDefinition>();
    public IReadOnlyList<GuideDefinition> Guides { get; init; } = Array.Empty<GuideDefinition>();
    public IReadOnlyList<BackgroundDefinition> Backgrounds { get; init; } = Array.Empty<BackgroundDefinition>();
    public IReadOnlyList<TemplateElement> Elements { get; init; } = Array.Empty<TemplateElement>();
    public IReadOnlyList<AssetReference> Assets { get; init; } = Array.Empty<AssetReference>();
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = Array.Empty<FieldDefinition>();
    public IReadOnlyList<SampleValue> SampleValues { get; init; } = Array.Empty<SampleValue>();
    public TemplatePrintSettings PrintSettings { get; init; } = new();

    public static TemplateDocument CreateNew(string title = "Untitled")
    {
        LayerDefinition defaultLayer = new(Guid.NewGuid(), "Default");
        return new TemplateDocument
        {
            Metadata = new DocumentMetadata { Title = title },
            Page = new PageDefinition
            {
                Media = new MediaDefinition("A4", new MmSize(210, 297)),
            },
            Layers = [defaultLayer],
        };
    }
}
