using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

public sealed class AddBackgroundCommand(BackgroundDefinition background) : IEditorCommand
{
    public string Description => "Add background";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(background);
        if (background.Id == Guid.Empty || document.Backgrounds.Any(item => item.Id == background.Id))
        {
            throw new InvalidOperationException("Background requires a unique non-empty identity.");
        }

        return document with { Backgrounds = document.Backgrounds.Append(background).ToArray() };
    }
}

public sealed class DeleteBackgroundsCommand(IEnumerable<Guid> backgroundIds) : IEditorCommand
{
    private readonly HashSet<Guid> backgroundIds = backgroundIds?.ToHashSet()
        ?? throw new ArgumentNullException(nameof(backgroundIds));

    public string Description => "Delete backgrounds";

    public TemplateDocument Execute(TemplateDocument document)
    {
        BackgroundDefinition[] retained = document.Backgrounds
            .Where(background => !backgroundIds.Contains(background.Id))
            .ToArray();
        return retained.Length == document.Backgrounds.Count
            ? document
            : document with { Backgrounds = retained };
    }
}

public sealed class ChangeBackgroundPropertiesCommand(
    Guid backgroundId,
    Func<BackgroundDefinition, BackgroundDefinition> transform,
    string? coalescingKey = null) : IEditorCommand
{
    private readonly Func<BackgroundDefinition, BackgroundDefinition> transform = transform
        ?? throw new ArgumentNullException(nameof(transform));

    public string Description => "Change background properties";

    public string? CoalescingKey { get; } = coalescingKey;

    public TemplateDocument Execute(TemplateDocument document)
    {
        bool changed = false;
        BackgroundDefinition[] backgrounds = document.Backgrounds.Select(background =>
        {
            if (background.Id != backgroundId)
            {
                return background;
            }

            BackgroundDefinition replacement = transform(background);
            if (replacement.Id != background.Id)
            {
                throw new InvalidOperationException("Background changes cannot replace identity.");
            }

            changed = !ReferenceEquals(replacement, background);
            return replacement;
        }).ToArray();
        return changed ? document with { Backgrounds = backgrounds } : document;
    }
}

public sealed class ReorderBackgroundsCommand(IReadOnlyList<Guid> orderedIds) : IEditorCommand
{
    private readonly IReadOnlyList<Guid> orderedIds = orderedIds
        ?? throw new ArgumentNullException(nameof(orderedIds));

    public string Description => "Reorder backgrounds";

    public TemplateDocument Execute(TemplateDocument document)
    {
        if (orderedIds.Count != document.Backgrounds.Count
            || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            throw new InvalidOperationException("Background order must contain each background exactly once.");
        }

        Dictionary<Guid, BackgroundDefinition> byId = document.Backgrounds.ToDictionary(static background => background.Id);
        if (orderedIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new InvalidOperationException("Background order contains an unknown identity.");
        }

        BackgroundDefinition[] reordered = orderedIds.Select(id => byId[id]).ToArray();
        return reordered.SequenceEqual(document.Backgrounds)
            ? document
            : document with { Backgrounds = reordered };
    }
}
