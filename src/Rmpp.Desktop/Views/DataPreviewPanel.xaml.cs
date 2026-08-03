using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class DataPreviewPanel : UserControl
{
    public DataPreviewPanel() { InitializeComponent(); }

    private void OnFieldMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox list || list.SelectedItem is not PreviewFieldValue field) return;
        if (Mouse.Captured is not null) return;
        DragDrop.DoDragDrop(list, new DataObject(DataFormats.StringFormat, $"[{field.Name}]"), DragDropEffects.Copy);
    }
}
