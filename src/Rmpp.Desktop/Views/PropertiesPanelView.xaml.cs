using Microsoft.Win32;
using System.Windows.Controls;
using Rmpp.Desktop.Resources;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class PropertiesPanelView : UserControl
{
    public PropertiesPanelView() => InitializeComponent();

    private async void OnSelectImage(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not DocumentTabViewModel document)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = DesktopText.Get("SelectImage"),
            Filter = DesktopText.Get("ImageFileFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() == true)
        {
            await document.Designer.ImportImageAsync(dialog.FileName);
        }
    }

    private void OnClearImage(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DocumentTabViewModel document)
        {
            document.Designer.ClearSelectedImage();
        }
    }
}
