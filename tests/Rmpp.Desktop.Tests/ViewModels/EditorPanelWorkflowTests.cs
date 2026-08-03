using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Rmpp.Desktop.ViewModels;
using Rmpp.Desktop.Views;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Desktop.Tests.ViewModels;

public sealed class EditorPanelWorkflowTests
{
    [Fact]
    public void DataPanelListsElementsAndSynchronizesSelectionWithoutImportedData()
    {
        TemplateDocument document = TemplateDocument.CreateNew("元素检查");
        TextElement element = new()
        {
            Name = "客户名称",
            LayerId = document.Layers[0].Id,
            Bounds = new MmRect(10, 10, 40, 12),
        };
        document = document with { Elements = [element] };
        using DocumentTabViewModel tab = new(document);

        PreviewElementItem item = Assert.Single(tab.DataPreview.Elements);
        Assert.Equal(element.Id, item.Id);
        Assert.Equal("客户名称", item.Name);

        tab.DataPreview.SelectedElement = item;

        Assert.Contains(element.Id, tab.Designer.SelectedElementIds);
    }

    [Fact]
    public void MovingGuideCommitsOneUndoableDocumentChange()
    {
        Guid guideId = Guid.NewGuid();
        TemplateDocument document = TemplateDocument.CreateNew("参考线") with
        {
            Guides = [new GuideDefinition(guideId, GuideOrientation.Horizontal, 20)],
        };
        using DocumentTabViewModel tab = new(document);

        tab.Designer.MoveGuide(guideId, 35);

        Assert.Equal(35, Assert.Single(tab.Designer.Document.Guides).PositionMm);
        Assert.True(tab.Dispatcher.History.CanUndo);
        tab.UndoCommand.Execute(null);
        Assert.Equal(20, Assert.Single(tab.Designer.Document.Guides).PositionMm);
    }

    [Fact]
    public void ExpressionFieldIsInsertedAtTheCurrentCaretOnSta()
    {
        RunOnSta(() =>
        {
            ExpressionEditorViewModel viewModel = new();
            viewModel.SetFields(["订单号"]);
            viewModel.Source = "=12";
            ExpressionEditorView view = new() { DataContext = viewModel };
            view.Measure(new Size(520, 420));
            view.Arrange(new Rect(0, 0, 520, 420));
            view.UpdateLayout();
            TextBox editor = Assert.IsType<TextBox>(view.FindName("Editor"));
            ListBox fields = Assert.IsType<ListBox>(view.FindName("ExpressionFieldsList"));
            fields.SelectedItem = "订单号";
            editor.CaretIndex = 2;

            MethodInfo insert = typeof(ExpressionEditorView).GetMethod(
                "InsertSelectedField",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("找不到字段插入方法。");
            insert.Invoke(view, null);

            Assert.Equal("=1[订单号]2", editor.Text);
            Assert.Equal("=1[订单号]2", viewModel.Source);
            Assert.Equal(7, editor.CaretIndex);
        });
    }

    [Fact]
    public void LayersPanelUsesExplainedOneWayStateControlsOnSta()
    {
        RunOnSta(() =>
        {
            using DocumentTabViewModel tab = new(TemplateDocument.CreateNew("图层状态"));
            LayersPanelView view = new() { DataContext = tab };
            view.Measure(new Size(360, 420));
            view.Arrange(new Rect(0, 0, 360, 420));
            view.UpdateLayout();
            CheckBox[] controls = Descendants<CheckBox>(view).ToArray();

            Assert.Equal(3, controls.Length);
            CheckBox output = Assert.Single(controls, control => AutomationProperties.GetName(control) == "图层输出");
            Assert.Contains("PDF", output.ToolTip?.ToString(), StringComparison.Ordinal);
            Assert.True(tab.Designer.Document.Layers[0].IsPrintable);

            Assert.NotNull(output.Command);
            output.Command.Execute(output.CommandParameter);

            Assert.False(tab.Designer.Document.Layers[0].IsPrintable);
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (T nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
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
