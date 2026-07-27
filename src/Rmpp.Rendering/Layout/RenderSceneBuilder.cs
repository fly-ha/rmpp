using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Fills;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Layout;

/// <summary>把文档和解析上下文转换为稳定排序、保留物理尺寸的单页场景。</summary>
public sealed class RenderSceneBuilder(ElementSceneBuilder? elementBuilder = null)
{
    private readonly ElementSceneBuilder elementBuilder = elementBuilder ?? new ElementSceneBuilder();

    public RenderScene Build(TemplateDocument document, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        RenderContext effectiveContext = context ?? new RenderContext();
        MmSize pageSize = GetOrientedSize(document.Page.Media);
        List<RenderCommand> commands = [];
        List<RenderIssue> issues = [];
        AddBackgrounds(document, effectiveContext, commands);

        Dictionary<Guid, LayerDefinition> layers = document.Layers.ToDictionary(static layer => layer.Id);
        foreach ((TemplateElementWithIndex item, LayerDefinition layer) in SelectElements(document, effectiveContext, layers, issues))
        {
            ElementSceneBuildResult result = elementBuilder.Build(item.Element, effectiveContext);
            commands.AddRange(result.Commands);
            issues.AddRange(result.Issues.Select(issue => issue with { PageNumber = 1 }));
        }

        RenderIssue[] frozenIssues = issues.ToArray();
        RenderPage page = new()
        {
            PageNumber = 1,
            Size = pageSize,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, pageSize.Width, pageSize.Height)),
            Commands = commands.ToArray(),
            Issues = frozenIssues,
        };
        return new RenderScene
        {
            DocumentId = document.Id,
            Pages = [page],
            Issues = frozenIssues,
        };
    }

    private static IEnumerable<(TemplateElementWithIndex Item, LayerDefinition Layer)> SelectElements(
        TemplateDocument document,
        RenderContext context,
        Dictionary<Guid, LayerDefinition> layers,
        List<RenderIssue> issues)
    {
        IEnumerable<TemplateElementWithIndex> ordered = document.Elements
            .Select(static (element, index) => new TemplateElementWithIndex(element, index))
            .OrderBy(static item => item.Element.ZIndex)
            .ThenBy(static item => item.Index);
        foreach (TemplateElementWithIndex item in ordered)
        {
            if (!layers.TryGetValue(item.Element.LayerId, out LayerDefinition? layer))
            {
                issues.Add(new RenderIssue(
                    "missing-layer",
                    "Element references a layer that does not exist.",
                    RenderIssueSeverity.Error,
                    item.Element.Id,
                    1));
                continue;
            }

            if (!layer.IsVisible || !item.Element.IsVisible)
            {
                continue;
            }

            if (context.RequiresPrintable && (!layer.IsPrintable || !item.Element.IsPrintable))
            {
                continue;
            }

            yield return (item, layer);
        }
    }

    private static void AddBackgrounds(
        TemplateDocument document,
        RenderContext context,
        List<RenderCommand> commands)
    {
        int order = 0;
        foreach (BackgroundDefinition background in document.Backgrounds)
        {
            if (!background.IsVisible || context.RequiresPrintable && !background.IsPrintable)
            {
                continue;
            }

            MmRect localBounds = new(0, 0, background.Bounds.Width, background.Bounds.Height);
            commands.Add(new RenderImageCommand
            {
                SourceId = background.Id,
                ZIndex = int.MinValue + order++,
                Opacity = background.Opacity,
                Transform = RenderTransform.ForElement(background.Bounds, Angle.Zero).Then(context.PlacementTransform),
                Clip = RenderClip.FromRectangle(localBounds),
                Image = new RenderImage
                {
                    AssetId = background.AssetId,
                    FitMode = background.FitMode,
                    PdfPageNumber = background.PdfPageNumber,
                },
                LocalBounds = localBounds,
                Border = RenderStroke.FromDomain(StrokeStyle.None),
                Fill = HatchPatternFactory.CreateFill(FillStyle.None),
            });
        }
    }

    private static MmSize GetOrientedSize(MediaDefinition media) =>
        media.Orientation == PageOrientation.Landscape
            ? new MmSize(media.Size.Height, media.Size.Width)
            : media.Size;

    private sealed record TemplateElementWithIndex(Rmpp.Domain.Elements.TemplateElement Element, int Index);
}
