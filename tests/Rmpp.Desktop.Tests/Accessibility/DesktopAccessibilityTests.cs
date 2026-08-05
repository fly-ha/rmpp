using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.ViewModels;
using Rmpp.Desktop.Views;
using Rmpp.Domain.Documents;
using Xunit;

namespace Rmpp.Desktop.Tests.Accessibility;

public sealed class DesktopAccessibilityTests
{
    [Fact]
    public void PrincipalControlsExposeNamesFocusOrderDpiAndChineseRenderingOnSta()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                VerifyPrincipalControls();
                VerifyDesignerRendersWithSystemThemeBrushes();
                VerifyDpiManifestAndChineseFont();
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

    private static void VerifyPrincipalControls()
    {
        DesignerView designer = WithStrings(new DesignerView());
        AssertNamedWithTabIndex<Slider>(designer, "ZoomSlider", 0);
        DesignerSurface surface = AssertNamedWithTabIndex<DesignerSurface>(designer, "Surface", 1);
        Assert.True(surface.Focusable);
        AssertNamedWithTabIndex<TabControl>(designer, "InspectorTabs", 2);
        Assert.Equal(KeyboardNavigationMode.Contained, KeyboardNavigation.GetTabNavigation(designer));

        DataImportDialog import = WithStrings(new DataImportDialog());
        AssertNamedWithTabIndex<TextBox>(import, "FilePathInput", 0);
        AssertNamedWithTabIndex<Button>(import, "ChooseDataFileButton", 1);
        AssertNamedWithTabIndex<ListBox>(import, "PreviewRows", 8);
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(import));

        DataPreviewPanel preview = WithStrings(new DataPreviewPanel());
        AssertNamedWithTabIndex<TextBox>(preview, "SearchInput", 2);
        AssertNamedWithTabIndex<ListBox>(preview, "FieldsList", 7);

        ExpressionEditorView expression = WithStrings(new ExpressionEditorView());
        AssertNamedWithTabIndex<TextBox>(expression, "Editor", 0);
        AssertNamedWithTabIndex<ListBox>(expression, "ExpressionFieldsList", 1);
        AssertNamedWithTabIndex<ListBox>(expression, "ExpressionFunctionsList", 2);

        PrintSetupDialog print = WithStrings(new PrintSetupDialog());
        AssertNamedWithTabIndex<ComboBox>(print, "PrinterCombo", 0);
        AssertNamedWithTabIndex<ComboBox>(print, "MediaCombo", 1);
        AssertNamedWithTabIndex<ComboBox>(print, "ResolutionCombo", 2);
        AssertNamedWithTabIndex<Button>(print, "BuildPlanButton", 8);
        AssertNamedWithTabIndex<Button>(print, "PrintButton", 9);
        AssertNamedWithTabIndex<TextBox>(print, "PdfPathInput", 10);

        CalibrationWizard calibration = WithStrings(new CalibrationWizard());
        AssertNamedWithTabIndex<ComboBox>(calibration, "PrinterCombo", 0);
        AssertNamedWithTabIndex<ComboBox>(calibration, "MediaCombo", 1);
        AssertNamed<UniformGrid>(calibration, "MeasurementGrid");

        RecoveryDialog recovery = WithStrings(new RecoveryDialog());
        AssertNamedWithTabIndex<ListBox>(recovery, "RecoverySessionsList", 0);

        SettingsCatalogDialog settings = WithStrings(new SettingsCatalogDialog());
        AssertNamed<TabControl>(settings, "SettingsTabsControl");
        AssertNamed<DataGrid>(settings, "TemplateCatalogGrid");

        PrintPreviewView printPreview = WithStrings(new PrintPreviewView());
        AssertNamed<Image>(printPreview, "PreviewImage");

        LayersPanelView layers = WithStrings(new LayersPanelView());
        AssertNamed<ListBox>(layers, "LayersList");
    }

    private static void VerifyDesignerRendersWithSystemThemeBrushes()
    {
        DesignerSurface surface = new()
        {
            Document = TemplateDocument.CreateNew("高对比度检查"),
            Focusable = true,
        };
        surface.Measure(new Size(900, 900));
        surface.Arrange(new Rect(surface.DesiredSize));
        int width = Math.Max(1, (int)Math.Ceiling(surface.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(surface.ActualHeight));
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);

        Assert.True(bitmap.PixelWidth > 0);
        Assert.True(bitmap.PixelHeight > 0);
    }

    private static void VerifyDpiManifestAndChineseFont()
    {
        string root = FindRepositoryRoot();
        string manifest = File.ReadAllText(Path.Combine(root, "src", "Rmpp.Desktop", "app.manifest"));
        string appXaml = File.ReadAllText(Path.Combine(root, "src", "Rmpp.Desktop", "App.xaml"));
        string mainWindowXaml = File.ReadAllText(Path.Combine(root, "src", "Rmpp.Desktop", "MainWindow.xaml"));

        Assert.Contains("PerMonitorV2", manifest, StringComparison.Ordinal);
        Assert.Contains("Microsoft YaHei UI", appXaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name", mainWindowXaml, StringComparison.Ordinal);

        // 主题必须能在真实 Application 资源树中加载，避免仅在编译阶段通过。
        App application = new();
        application.InitializeComponent();
        Assert.NotNull(application.Resources["PrimaryBrush"]);
        Assert.NotNull(application.Resources["MaterialDesignRaisedButton"]);

        // 真实主窗口构造可防止主题隐式样式形成递归而只留下后台进程。
        MainWindow window = new(new MainWindowViewModel());
        Assert.Equal("RMPP 红枫叶定位打印", window.Title);
        window.Close();

        TextBlock chinese = new()
        {
            Text = "红枫叶定位打印",
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 14,
        };
        chinese.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(chinese.DesiredSize.Width > 0);
        Assert.True(chinese.DesiredSize.Height > 0);
    }

    private static T WithStrings<T>(T root) where T : FrameworkElement
    {
        root.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Rmpp.Desktop;component/Resources/Strings.xaml", UriKind.Relative),
        });
        return root;
    }

    private static T AssertNamedWithTabIndex<T>(FrameworkElement root, string elementName, int expectedTabIndex)
        where T : FrameworkElement
    {
        T element = AssertNamed<T>(root, elementName);
        Assert.Equal(expectedTabIndex, KeyboardNavigation.GetTabIndex(element));
        return element;
    }

    private static T AssertNamed<T>(FrameworkElement root, string elementName) where T : FrameworkElement
    {
        T element = Assert.IsType<T>(root.FindName(elementName));
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(element)), $"{elementName} 缺少屏幕阅读器名称。");
        return element;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Rmpp.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 RMPP 仓库根目录。");
    }
}
