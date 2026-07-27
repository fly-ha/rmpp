using CommunityToolkit.Mvvm.ComponentModel;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Application.Editing.Snapping;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Desktop.Resources;

namespace Rmpp.Desktop.ViewModels;

/// <summary>保存纯视图设置，并把所有编辑动作转为应用命令。</summary>
public sealed class DesignerViewModel : ObservableObject, IDisposable
{
    private readonly DocumentSession session;
    private readonly EditorCommandDispatcher dispatcher;
    private double zoom = 1;
    private bool showGrid = true;
    private bool snappingEnabled = true;
    private double gridSpacingMm = 5;
    private string snapIndicator = string.Empty;

    public DesignerViewModel(DocumentSession session, EditorCommandDispatcher dispatcher)
    {
        this.session = session;
        this.dispatcher = dispatcher;
        session.Changed += OnSessionChanged;
    }

    public TemplateDocument Document => session.State.Document;
    public IReadOnlySet<Guid> SelectedElementIds => session.State.SelectedElementIds;

    public double Zoom
    {
        get => zoom;
        set => SetProperty(ref zoom, Math.Clamp(value, 0.1, 8));
    }

    public bool ShowGrid { get => showGrid; set => SetProperty(ref showGrid, value); }
    public bool SnappingEnabled { get => snappingEnabled; set => SetProperty(ref snappingEnabled, value); }
    public double GridSpacingMm { get => gridSpacingMm; set => SetProperty(ref gridSpacingMm, Math.Clamp(value, 0.1, 100)); }
    public string SnapIndicator { get => snapIndicator; private set => SetProperty(ref snapIndicator, value); }

    public void Select(Guid? elementId, bool additive)
    {
        if (elementId is null)
        {
            session.SetSelection(Array.Empty<Guid>());
            return;
        }

        HashSet<Guid> selection = additive ? session.State.SelectedElementIds.ToHashSet() : [];
        if (!selection.Add(elementId.Value) && additive)
        {
            selection.Remove(elementId.Value);
        }

        session.SetSelection(selection);
    }

    public void MoveSelection(double deltaXmm, double deltaYmm, bool temporarilyDisableSnap = false)
    {
        if (session.State.SelectedElementIds.Count == 0)
        {
            return;
        }

        TemplateElement? anchor = session.State.Document.Elements.FirstOrDefault(
            element => session.State.SelectedElementIds.Contains(element.Id));
        MmPoint adjusted = new(deltaXmm, deltaYmm);
        if (anchor is not null)
        {
            SnapResult snap = SnapEngine.Snap(
                anchor.Bounds,
                adjusted,
                new SnapContext(
                    Document.Page.Media.Size,
                    Document.Guides,
                    Document.Elements,
                    session.State.SelectedElementIds),
                new SnapOptions
                {
                    IsEnabled = SnappingEnabled,
                    IsTemporarilyDisabled = temporarilyDisableSnap,
                    GridSpacingMm = GridSpacingMm,
                    PixelsPerMillimetre = 96d / 25.4 * Zoom,
                });
            adjusted = snap.AdjustedDelta;
            SnapIndicator = snap.Snapped
                ? string.Join(" / ", new[] { snap.HorizontalMatch?.Candidate.Label, snap.VerticalMatch?.Candidate.Label }.Where(static label => label is not null))
                : string.Empty;
        }

        _ = dispatcher.Execute(new MoveElementsCommand(
            session.State.SelectedElementIds,
            adjusted.X,
            adjusted.Y,
            "designer-move"));
    }

    public void ResizePrimary(MmRect bounds)
    {
        Guid? id = SelectedElementIds.FirstOrDefault();
        if (id is { } elementId && elementId != Guid.Empty)
        {
            _ = dispatcher.Execute(new ResizeElementsCommand(new Dictionary<Guid, MmRect> { [elementId] = bounds }, "designer-resize"));
        }
    }

    public void RotatePrimary(Angle angle)
    {
        Guid? id = SelectedElementIds.FirstOrDefault();
        if (id is { } elementId && elementId != Guid.Empty)
        {
            _ = dispatcher.Execute(new RotateElementsCommand(new Dictionary<Guid, Angle> { [elementId] = angle }, "designer-rotate"));
        }
    }

    public void EditPrimaryPoints(IReadOnlyList<MmPoint> points)
    {
        Guid? id = SelectedElementIds.FirstOrDefault();
        if (id is { } elementId && elementId != Guid.Empty)
        {
            _ = dispatcher.Execute(new EditPointsCommand(elementId, points, "designer-points"));
        }
    }

    public void DuplicateSelection()
    {
        TemplateElement[] copies = Document.Elements
            .Where(element => SelectedElementIds.Contains(element.Id))
            .Select(element => element with
            {
                Id = Guid.NewGuid(),
                Name = element.Name + DesktopText.Get("DuplicateSuffix"),
                Bounds = element.Bounds.Translate(5, 5),
            })
            .ToArray();
        if (copies.Length > 0 && dispatcher.Execute(new AddElementsCommand(copies)))
        {
            session.SetSelection(copies.Select(static element => element.Id));
        }
    }

    public void Align(AlignmentMode mode)
    {
        IReadOnlyDictionary<Guid, MmRect> bounds = AlignmentService.Calculate(Document, SelectedElementIds, mode);
        _ = dispatcher.Execute(new ResizeElementsCommand(bounds));
    }

    public void Distribute(DistributionAxis axis)
    {
        IReadOnlyDictionary<Guid, MmRect> bounds = DistributionService.Calculate(Document, SelectedElementIds, axis);
        _ = dispatcher.Execute(new ResizeElementsCommand(bounds));
    }

    public void AddGuide(GuideOrientation orientation, double positionMm)
    {
        GuideDefinition[] guides = Document.Guides.Append(new GuideDefinition(Guid.NewGuid(), orientation, positionMm)).ToArray();
        _ = dispatcher.Execute(new ChangeGuidesCommand(guides));
    }

    public void ClearGuides() => _ = dispatcher.Execute(new ChangeGuidesCommand(Array.Empty<GuideDefinition>()));

    private void OnSessionChanged(object? sender, DocumentSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(SelectedElementIds));
    }

    public void Dispose() => session.Changed -= OnSessionChanged;
}
