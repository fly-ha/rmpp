using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Validation;

/// <summary>检查图层身份唯一性以及每个元素的图层引用。</summary>
public sealed class LayerValidationRule : IDocumentValidationRule
{
    public IEnumerable<ValidationIssue> Validate(DocumentValidationContext context)
    {
        HashSet<Guid> layerIds = [];
        foreach (LayerDefinition layer in context.Document.Layers)
        {
            if (!layerIds.Add(layer.Id))
            {
                yield return new ValidationIssue(
                    "duplicate-layer-id",
                    "Layer identity is duplicated.",
                    ValidationSeverity.Error,
                    new ValidationLocation
                    {
                        DocumentId = context.Document.Id,
                        LayerId = layer.Id,
                    });
            }
        }

        foreach (TemplateElement element in context.Document.Elements.Where(element => !layerIds.Contains(element.LayerId)))
        {
            yield return new ValidationIssue(
                "missing-layer",
                "Element references a layer that does not exist.",
                ValidationSeverity.Error,
                new ValidationLocation
                {
                    DocumentId = context.Document.Id,
                    PageNumber = 1,
                    ElementId = element.Id,
                    LayerId = element.LayerId,
                    PropertyPath = nameof(element.LayerId),
                });
        }
    }
}
