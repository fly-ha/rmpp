using System.IO;
using Rmpp.Application.Editing.Snapping;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Desktop.Tests.ViewModels;

public sealed class DesktopViewModelTests
{
    [Fact]
    public void MainWindowCreatesAndSwitchesIndependentDocumentTabs()
    {
        MainWindowViewModel viewModel = new();
        DocumentTabViewModel first = Assert.Single(viewModel.Documents);

        viewModel.NewDocumentCommand.Execute(null);

        Assert.Equal(2, viewModel.Documents.Count);
        Assert.NotSame(first.Session, viewModel.ActiveDocument!.Session);
        Assert.NotSame(first.Dispatcher.History, viewModel.ActiveDocument.Dispatcher.History);
    }

    [Fact]
    public void SelectionMoveAndPropertyCoordinateUseApplicationCommands()
    {
        TemplateDocument document = CreateDocumentWithElements(out RectangleElement lower, out _);
        using DocumentTabViewModel tab = new(document);
        tab.Designer.Select(lower.Id, false);

        tab.Designer.MoveSelection(3, 4);
        tab.Properties.X = 20;

        RectangleElement moved = Assert.IsType<RectangleElement>(tab.Session.State.Document.Elements.Single(element => element.Id == lower.Id));
        Assert.Equal(20, moved.Bounds.X);
        Assert.Equal(15, moved.Bounds.Y);
        Assert.Contains("grid", tab.Designer.SnapIndicator, StringComparison.Ordinal);
        Assert.True(tab.IsDirty);
        Assert.True(tab.Dispatcher.History.CanUndo);
    }

    [Fact]
    public void HitTesterReturnsHighestVisibleElement()
    {
        TemplateDocument document = CreateDocumentWithElements(out _, out RectangleElement upper);

        TemplateElement? hit = ElementHitTester.HitTest(document, new MmPoint(15, 15));

        Assert.Equal(upper.Id, hit!.Id);
    }

    [Fact]
    public void DuplicateResizeRotateAlignAndDistributeRemainUndoable()
    {
        TemplateDocument document = CreateDocumentWithElements(out RectangleElement lower, out RectangleElement upper);
        using DocumentTabViewModel tab = new(document);
        tab.Designer.Select(lower.Id, false);
        tab.Designer.DuplicateSelection();
        Guid copyId = Assert.Single(tab.Session.State.SelectedElementIds);
        Assert.NotEqual(lower.Id, copyId);

        tab.Designer.ResizePrimary(new MmRect(15, 15, 30, 40));
        tab.Designer.RotatePrimary(new Angle(45));
        tab.Designer.Select(upper.Id, true);
        tab.Designer.Align(AlignmentMode.Left);

        Assert.True(tab.Dispatcher.History.CanUndo);
        Assert.Contains(tab.Session.State.Document.Elements, element => element.Id == copyId && element.Rotation == new Angle(45));
    }

    [Fact]
    public void TypePropertiesAndLayerControlsUpdateTheSharedSession()
    {
        TemplateDocument document = CreateDocumentWithElements(out RectangleElement lower, out _);
        using DocumentTabViewModel tab = new(document);
        tab.Designer.Select(lower.Id, false);

        tab.Properties.FillMode = "Hatch";
        tab.Properties.HatchPattern = Rmpp.Domain.Styles.HatchPattern.Grid;
        tab.Properties.StrokeWidth = 0.5;
        tab.Layers.ToggleVisibleCommand.Execute(document.Layers[0]);

        RectangleElement updated = Assert.IsType<RectangleElement>(tab.Session.State.Document.Elements.Single(element => element.Id == lower.Id));
        Assert.Equal(0.5, updated.Stroke.WidthMm);
        Assert.Equal(Rmpp.Domain.Styles.HatchPattern.Grid, Assert.IsType<HatchFill>(updated.Fill).Pattern);
        Assert.False(tab.Session.State.Document.Layers[0].IsVisible);
    }

    [Fact]
    public void DesignerSurfaceAcceptsDocumentSelectionAndZoomOnStaThread()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                TemplateDocument document = CreateDocumentWithElements(out RectangleElement lower, out _);
                DesignerSurface surface = new()
                {
                    Document = document,
                    SelectedElementIds = new HashSet<Guid> { lower.Id },
                    Zoom = 2,
                };
                surface.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(surface.DesiredSize.Width > 210 * 2);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    public static TheoryData<DesignerTool, Type> CreatableTools => new()
    {
        { DesignerTool.Text, typeof(TextElement) },
        { DesignerTool.DateTime, typeof(DateTimeElement) },
        { DesignerTool.Serial, typeof(SerialElement) },
        { DesignerTool.Image, typeof(ImageElement) },
        { DesignerTool.Barcode, typeof(BarcodeElement) },
        { DesignerTool.QrCode, typeof(BarcodeElement) },
        { DesignerTool.DataMatrix, typeof(BarcodeElement) },
        { DesignerTool.Line, typeof(LineElement) },
        { DesignerTool.Rectangle, typeof(RectangleElement) },
        { DesignerTool.RoundedRectangle, typeof(RectangleElement) },
        { DesignerTool.Ellipse, typeof(EllipseElement) },
        { DesignerTool.Arc, typeof(ArcElement) },
        { DesignerTool.Sector, typeof(SectorElement) },
        { DesignerTool.Polyline, typeof(PolylineElement) },
        { DesignerTool.Polygon, typeof(PolygonElement) },
    };

    [Theory]
    [MemberData(nameof(CreatableTools))]
    public void EveryToolCreatesAValidSelectedUndoableElement(DesignerTool tool, Type expectedType)
    {
        using DocumentTabViewModel tab = new(TemplateDocument.CreateNew("工具创建"));
        tab.Designer.ActiveTool = tool;

        tab.Designer.CreateElement(tool, new MmRect(10, 20, 30, 15));

        TemplateElement created = Assert.Single(tab.Session.State.Document.Elements);
        Assert.IsType(expectedType, created);
        Assert.Equal(new MmRect(10, 20, 30, 15), created.Bounds);
        Assert.Contains(created.Id, tab.Session.State.SelectedElementIds);
        Assert.Equal(DesignerTool.Select, tab.Designer.ActiveTool);
        _ = created.Validate();
        Assert.True(tab.Dispatcher.Undo());
        Assert.Empty(tab.Session.State.Document.Elements);
        Assert.True(tab.Dispatcher.Redo());
        Assert.Single(tab.Session.State.Document.Elements);
    }

    [Fact]
    public async Task ImportedBackgroundIsPackagedReferenceOnlyAndUndoable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rmpp-background-{Guid.NewGuid():N}.png");
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(path, png);
        try
        {
            using DocumentTabViewModel tab = new(TemplateDocument.CreateNew("套打背景"));

            await tab.Designer.ImportBackgroundAsync(path);

            BackgroundDefinition background = Assert.Single(tab.Session.State.Document.Backgrounds);
            AssetReference asset = Assert.Single(tab.Session.State.Document.Assets);
            Assert.Equal(asset.Id, background.AssetId);
            Assert.True(background.IsVisible);
            Assert.True(background.IsLocked);
            Assert.False(background.IsPrintable);
            Assert.Equal(tab.Session.State.Document.Page.Media.Size, background.Bounds.Size);
            Assert.Equal(png, tab.Session.State.AssetContents[asset.Id].ToArray());

            TemplatePackageContent recovery = tab.CreateRecoveryContent();
            Assert.Equal(png, Assert.Single(recovery.Assets).Value);

            tab.Designer.ActiveBackgroundPrintable = true;
            Assert.True(Assert.Single(tab.Session.State.Document.Backgrounds).IsPrintable);
            Assert.True(tab.Dispatcher.Undo());
            Assert.False(Assert.Single(tab.Session.State.Document.Backgrounds).IsPrintable);

            tab.Designer.DeleteActiveBackground();
            Assert.Empty(tab.Session.State.Document.Backgrounds);
            Assert.Empty(tab.Session.State.Document.Assets);
            Assert.True(tab.Dispatcher.Undo());
            Assert.Single(tab.Session.State.Document.Backgrounds);
            Assert.Single(tab.CreateRecoveryContent().Assets);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExistingPackageAssetsEnterTheDocumentSessionAndDesignerSurface()
    {
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        Guid assetId = Guid.NewGuid();
        AssetReference asset = new(assetId, "background.png", "image/png", AssetHashService.ComputeSha256(png));
        TemplateDocument document = TemplateDocument.CreateNew("已有资源") with
        {
            Assets = [asset],
            Backgrounds =
            [
                new BackgroundDefinition
                {
                    AssetId = assetId,
                    Bounds = new MmRect(0, 0, 210, 297),
                },
            ],
        };
        using DocumentTabViewModel tab = new(document, new Dictionary<Guid, byte[]> { [assetId] = png });

        Assert.DoesNotContain(tab.Session.State.ValidationIssues, issue => issue.Code == "missing-asset-content");
        Assert.Equal(png, tab.Designer.AssetContents[assetId].ToArray());
        Assert.Equal(png, tab.Assets[assetId]);
    }

    private static TemplateDocument CreateDocumentWithElements(
        out RectangleElement lower,
        out RectangleElement upper)
    {
        TemplateDocument document = TemplateDocument.CreateNew("测试");
        Guid layer = document.Layers[0].Id;
        lower = new RectangleElement
        {
            Name = "下层",
            LayerId = layer,
            ZIndex = 1,
            Bounds = new MmRect(10, 10, 20, 20),
        };
        upper = new RectangleElement
        {
            Name = "上层",
            LayerId = layer,
            ZIndex = 2,
            Bounds = new MmRect(12, 12, 20, 20),
        };
        return document with { Elements = [lower, upper] };
    }
}
