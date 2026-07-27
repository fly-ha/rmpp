using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

internal static class ElementCommandUtilities
{
    public static TemplateDocument Map(
        TemplateDocument document,
        IReadOnlySet<Guid> ids,
        Func<TemplateElement, TemplateElement> transform)
    {
        bool changed = false;
        TemplateElement[] elements = document.Elements.Select(element =>
        {
            if (!ids.Contains(element.Id))
            {
                return element;
            }

            TemplateElement replacement = transform(element);
            changed |= !ReferenceEquals(replacement, element);
            return replacement;
        }).ToArray();
        return changed ? document with { Elements = elements } : document;
    }

    public static HashSet<Guid> NormalizeIds(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        HashSet<Guid> result = ids.ToHashSet();
        if (result.Contains(Guid.Empty))
        {
            throw new ArgumentException("Element identities cannot be empty.", nameof(ids));
        }

        return result;
    }
}
