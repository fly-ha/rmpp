using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Rmpp.Application.Editing.Snapping;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;

namespace Rmpp.Desktop.Views;

public partial class DesignerView : UserControl
{
    private System.Windows.Point? toolboxDragStart;

    public DesignerView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not DocumentTabViewModel document || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            double current = document.Properties.Rotation ?? 0;
            document.Designer.RotatePrimary(new Angle(current + Math.Sign(e.Delta)));
        }
        else
        {
            document.Designer.Zoom *= e.Delta > 0 ? 1.1 : 1 / 1.1;
        }
        e.Handled = true;
    }

    private void OnElementSelected(object sender, ElementSelectedEventArgs e)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            document.Designer.Select(e.ElementId, e.IsAdditive);
        }
    }

    private void OnElementCreateRequested(object sender, ElementCreateRequestedEventArgs e)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            document.Designer.CreateElement(e.Tool, e.Bounds);
        }
    }

    private void OnToolboxMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        toolboxDragStart = e.GetPosition(ToolboxList);

    private void OnToolboxMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || toolboxDragStart is not { } start
            || ToolboxList.SelectedItem is not DesignerToolItem item
            || item.Tool == DesignerTool.Select)
        {
            return;
        }

        System.Windows.Point current = e.GetPosition(ToolboxList);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DataObject data = new();
        data.SetData(DesignerSurface.DesignerToolDataFormat, item.Tool.ToString());
        _ = DragDrop.DoDragDrop(ToolboxList, data, DragDropEffects.Copy);
        toolboxDragStart = null;
    }

    private async void OnImportBackground(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not DocumentTabViewModel document)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = DesktopText.Get("ImportBackground"),
            Filter = DesktopText.Get("BackgroundFileFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() == true)
        {
            await document.Designer.ImportBackgroundAsync(dialog.FileName);
        }
    }

    private void OnDeleteBackground(object sender, System.Windows.RoutedEventArgs e) =>
        WithDesigner(static designer => designer.DeleteActiveBackground());

    private void OnElementsMoved(object sender, ElementsMovedEventArgs e)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            document.Designer.MoveSelection(e.DeltaXmm, e.DeltaYmm, Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        }
    }

    private void OnElementResized(object sender, ElementResizedEventArgs e)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            document.Designer.ResizePrimary(e.Bounds);
        }
    }

    private void OnGuideMoved(object sender, GuideMovedEventArgs e)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            document.Designer.MoveGuide(e.GuideId, e.PositionMm);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not DocumentTabViewModel document)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
        {
            document.DuplicateCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            document.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
        {
            document.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Delete)
        {
            document.DeleteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 1;
        (double x, double y) = e.Key switch
        {
            Key.Left => (-step, 0d),
            Key.Right => (step, 0d),
            Key.Up => (0d, -step),
            Key.Down => (0d, step),
            _ => (0d, 0d),
        };
        if (x != 0 || y != 0)
        {
            document.Designer.MoveSelection(x, y);
            e.Handled = true;
        }
    }

    private void OnAlignLeft(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.Align(AlignmentMode.Left));
    private void OnAlignCenter(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.Align(AlignmentMode.HorizontalCenter));
    private void OnAlignTop(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.Align(AlignmentMode.Top));
    private void OnDistributeHorizontal(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.Distribute(DistributionAxis.Horizontal));
    private void OnDistributeVertical(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.Distribute(DistributionAxis.Vertical));
    private void OnAddHorizontalGuide(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(designer => designer.AddGuide(GuideOrientation.Horizontal, designer.PageHeightMm / 2));
    private void OnAddVerticalGuide(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(designer => designer.AddGuide(GuideOrientation.Vertical, designer.PageWidthMm / 2));
    private void OnClearGuides(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.ClearGuides());

    private void WithDesigner(Action<DesignerViewModel> action)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            action(document.Designer);
        }
    }
}
