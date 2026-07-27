using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

/// <summary>修改图层名称、可见、可打印和锁定状态，并保持图层身份与顺序不变。</summary>
public sealed class ChangeLayerPropertiesCommand(
    Guid layerId,
    Func<LayerDefinition, LayerDefinition> transform) : IEditorCommand
{
    private readonly Func<LayerDefinition, LayerDefinition> transform = transform
        ?? throw new ArgumentNullException(nameof(transform));

    public string Description => "Change layer properties";

    public TemplateDocument Execute(TemplateDocument document)
    {
        LayerDefinition[] layers = document.Layers.ToArray();
        int index = Array.FindIndex(layers, layer => layer.Id == layerId);
        if (index < 0)
        {
            return document;
        }

        LayerDefinition replacement = transform(layers[index]);
        if (replacement.Id != layerId)
        {
            throw new InvalidOperationException("Layer property changes cannot replace identity.");
        }

        layers[index] = replacement;
        return document with { Layers = layers };
    }
}
