using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Printing;

/// <summary>按需把一个计划物理页合成为共享场景，并用 LRU 保持有界页面缓存。</summary>
public sealed class PrintPreviewService
{
    private readonly RenderSceneBuilder sceneBuilder;
    private readonly int maximumCachedPages;
    private readonly object gate = new();
    private readonly Dictionary<(Guid JobId, int PageNumber, RenderTarget Target), CacheEntry> cache = [];
    private readonly LinkedList<(Guid JobId, int PageNumber, RenderTarget Target)> lru = [];
    private int builtPageCount;

    public PrintPreviewService(RenderSceneBuilder? sceneBuilder = null, int maximumCachedPages = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCachedPages);
        this.sceneBuilder = sceneBuilder ?? new RenderSceneBuilder();
        this.maximumCachedPages = maximumCachedPages;
    }

    public int CachedPageCount
    {
        get
        {
            lock (gate)
            {
                return cache.Count;
            }
        }
    }

    public int BuiltPageCount => Volatile.Read(ref builtPageCount);

    public Task<RenderScene> GetPageAsync(
        PrintJobPlan plan,
        int pageIndex,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(plan, pageIndex, RenderTarget.Preview, cancellationToken);

    public Task<RenderScene> GetPageAsync(
        PrintJobPlan plan,
        int pageIndex,
        RenderTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (pageIndex < 0 || pageIndex >= plan.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        PlannedPhysicalPage page = plan.Pages[pageIndex];
        (Guid JobId, int PageNumber, RenderTarget Target) key = (plan.Context.JobId, page.PageNumber, target);
        lock (gate)
        {
            if (cache.TryGetValue(key, out CacheEntry? existing))
            {
                Touch(existing);
                return Task.FromResult(existing.Scene);
            }
        }

        return BuildAndCacheAsync(plan, page, target, key, cancellationToken);
    }

    public void Clear()
    {
        lock (gate)
        {
            cache.Clear();
            lru.Clear();
        }
    }

    private async Task<RenderScene> BuildAndCacheAsync(
        PrintJobPlan plan,
        PlannedPhysicalPage page,
        RenderTarget target,
        (Guid JobId, int PageNumber, RenderTarget Target) key,
        CancellationToken cancellationToken)
    {
        RenderScene scene = await Task.Run(() => BuildPage(plan, page, target, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (cache.TryGetValue(key, out CacheEntry? existing))
            {
                Touch(existing);
                return existing.Scene;
            }

            LinkedListNode<(Guid JobId, int PageNumber, RenderTarget Target)> node = lru.AddFirst(key);
            cache[key] = new CacheEntry(scene, node);
            while (cache.Count > maximumCachedPages && lru.Last is not null)
            {
                (Guid JobId, int PageNumber, RenderTarget Target) oldest = lru.Last.Value;
                lru.RemoveLast();
                cache.Remove(oldest);
            }
        }

        return scene;
    }

    private RenderScene BuildPage(
        PrintJobPlan plan,
        PlannedPhysicalPage page,
        RenderTarget target,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref builtPageCount);
        List<RenderCommand> commands = [];
        List<RenderIssue> issues = [];
        foreach (PlannedPlacement placement in page.Placements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderScene placementScene = sceneBuilder.Build(plan.DocumentSnapshot, new RenderContext
            {
                Target = target,
                ReferenceTime = plan.Context.JobTime.ReferenceTime,
                PlacementTransform = placement.Transform,
                ResolvedElements = placement.ResolvedElements.ToDictionary(static item => item.ElementId),
            });
            RenderPage placementPage = placementScene.Pages[0];
            commands.AddRange(placementPage.Commands);
            issues.AddRange(placementPage.Issues.Select(issue => issue with { PageNumber = page.PageNumber }));
        }

        RenderIssue[] frozenIssues = issues.ToArray();
        RenderPage renderPage = new()
        {
            PageNumber = page.PageNumber,
            Size = page.Size,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, page.Size.Width, page.Size.Height)),
            Commands = commands.ToArray(),
            Issues = frozenIssues,
        };
        return new RenderScene
        {
            DocumentId = plan.DocumentSnapshot.Id,
            Pages = [renderPage],
            Issues = frozenIssues,
        };
    }

    private void Touch(CacheEntry entry)
    {
        lru.Remove(entry.Node);
        lru.AddFirst(entry.Node);
    }

    private sealed record CacheEntry(
        RenderScene Scene,
        LinkedListNode<(Guid JobId, int PageNumber, RenderTarget Target)> Node);
}
