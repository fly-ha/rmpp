using System.Windows.Controls;
using System.Windows.Input;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.ViewModels;
using Rmpp.Application.Editing.Snapping;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;

namespace Rmpp.Desktop.Views;

public partial class DesignerView : UserControl
{
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
    private void OnAddHorizontalGuide(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(designer => designer.AddGuide(GuideOrientation.Horizontal, designer.Document.Page.Media.Size.Height / 2));
    private void OnClearGuides(object sender, System.Windows.RoutedEventArgs e) => WithDesigner(static designer => designer.ClearGuides());

    private void WithDesigner(Action<DesignerViewModel> action)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            action(document.Designer);
        }
    }
}
