using System.Windows;
using System.Windows.Controls;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.ViewModels;
using Rmpp.Desktop.Views;
using Rmpp.Domain.Documents;
using Xunit;

namespace Rmpp.Desktop.Tests.Controls;

public sealed class DesignerToolboxInteractionTests
{
    [Fact]
    public void ToolboxSelectionUpdatesActiveToolAndSurfaceAcceptsDropsOnSta()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using DocumentTabViewModel tab = new(TemplateDocument.CreateNew("工具箱交互"));
                DesignerView view = new() { DataContext = tab };
                view.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/Rmpp.Desktop;component/Resources/Strings.xaml", UriKind.Relative),
                });
                view.Measure(new Size(1280, 820));
                view.Arrange(new Rect(0, 0, 1280, 820));
                view.UpdateLayout();

                ListBox toolbox = Assert.IsType<ListBox>(view.FindName("ToolboxList"));
                DesignerSurface surface = Assert.IsType<DesignerSurface>(view.FindName("Surface"));
                Assert.Equal(16, toolbox.Items.Count);
                Assert.True(surface.AllowDrop);

                DesignerToolItem textTool = Assert.IsType<DesignerToolItem>(toolbox.Items[1]);
                toolbox.SelectedItem = textTool;

                Assert.Equal(DesignerTool.Text, tab.Designer.ActiveTool);
                Assert.Equal(DesignerTool.Text, surface.ActiveTool);
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
}
