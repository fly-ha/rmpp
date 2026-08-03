using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

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

/// <summary>把背景定义与其包内资源引用作为一个可撤销文档变更加入，避免出现背景指向不存在资源的中间状态。</summary>
public sealed class AddBackgroundAssetCommand(
    AssetReference asset,
    BackgroundDefinition background) : IEditorCommand
{
    public string Description => "Add background asset";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(background);
        if (asset.Id == Guid.Empty || document.Assets.Any(item => item.Id == asset.Id))
        {
            throw new InvalidOperationException("Background asset requires a unique non-empty identity.");
        }

        if (background.AssetId != asset.Id)
        {
            throw new InvalidOperationException("Background must reference the imported asset.");
        }

        TemplateDocument withAsset = document with
        {
            Assets = document.Assets.Append(asset).ToArray(),
        };
        return new AddBackgroundCommand(background).Execute(withAsset);
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

/// <summary>删除一个背景，并在没有其他元素引用时同步移除其资源元数据；资源字节由会话保留以支持重做。</summary>
public sealed class DeleteBackgroundAssetCommand(Guid backgroundId) : IEditorCommand
{
    public string Description => "Delete background asset";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        BackgroundDefinition? target = document.Backgrounds.FirstOrDefault(item => item.Id == backgroundId);
        if (target is null)
        {
            return document;
        }

        BackgroundDefinition[] backgrounds = document.Backgrounds
            .Where(item => item.Id != backgroundId)
            .ToArray();
        bool remainsReferenced = backgrounds.Any(item => item.AssetId == target.AssetId)
            || document.Elements.OfType<ImageElement>().Any(item => item.AssetId == target.AssetId);
        AssetReference[] assets = remainsReferenced
            ? document.Assets.ToArray()
            : document.Assets.Where(item => item.Id != target.AssetId).ToArray();
        return document with { Backgrounds = backgrounds, Assets = assets };
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
