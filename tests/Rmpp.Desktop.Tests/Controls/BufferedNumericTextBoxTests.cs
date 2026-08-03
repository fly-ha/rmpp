using System.Windows.Data;
using System.Windows.Automation;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Desktop.Tests.Controls;

public sealed class BufferedNumericTextBoxTests
{
    [Fact]
    public void IntermediateDecimalTextDoesNotChangeTheDocumentUntilCommitOnSta()
    {
        RunOnSta(() =>
        {
            TemplateDocument document = TemplateDocument.CreateNew("数字缓冲");
            RectangleElement rectangle = new()
            {
                LayerId = document.Layers[0].Id,
                Bounds = new MmRect(10, 10, 20, 20),
            };
            document = document with { Elements = [rectangle] };
            using DocumentTabViewModel tab = new(document);
            tab.Designer.Select(rectangle.Id, false);
            BufferedNumericTextBox editor = new();
            BindingOperations.SetBinding(editor, BufferedNumericTextBox.ValueProperty, new Binding(nameof(PropertiesViewModel.X))
            {
                Source = tab.Properties,
                Mode = BindingMode.TwoWay,
            });

            editor.Text = "1.";

            Assert.Equal("1.", editor.Text);
            Assert.Equal(10, Assert.IsType<RectangleElement>(tab.Designer.Document.Elements[0]).Bounds.X);

            editor.Text = "1.25";
            Assert.True(editor.Commit());
            Assert.Equal(1.25, Assert.IsType<RectangleElement>(tab.Designer.Document.Elements[0]).Bounds.X);
        });
    }

    [Fact]
    public void InvalidRangeKeepsThePreviousValueAndEscapeRestoresTextOnSta()
    {
        RunOnSta(() =>
        {
            BufferedNumericTextBox editor = new() { Value = 0.5, Minimum = 0.01, Maximum = 1 };
            editor.Text = "0";

            Assert.False(editor.Commit());
            Assert.Equal(0.5, editor.Value);
            Assert.NotEmpty(AutomationProperties.GetHelpText(editor));

            editor.Text = "-";
            editor.CancelEdit();
            Assert.Equal("0.5", editor.Text);
            Assert.Empty(AutomationProperties.GetHelpText(editor));
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
