using System.Windows.Automation;
using System.Windows.Controls;
using Rmpp.Desktop.Controls;
using Xunit;

namespace Rmpp.Desktop.Tests.Controls;

public sealed class RgbaColorEditorTests
{
    [Fact]
    public void ManualPaletteAlphaAndEscapePathsRemainBufferedOnSta()
    {
        RunOnSta(() =>
        {
            RgbaColorEditor editor = new() { Value = "#80402010" };
            TextBox textBox = Assert.IsType<TextBox>(editor.FindName("ColorTextBox"));

            textBox.Text = "#40112233";
            Assert.True(editor.Commit());
            Assert.Equal("#40112233", editor.Value);

            editor.ApplyPaletteColor("#FF0000");
            Assert.Equal("#40FF0000", editor.Value);

            editor.ApplyAlpha(0x7F);
            Assert.Equal("#7FFF0000", editor.Value);

            textBox.Text = "#invalid";
            Assert.False(editor.Commit());
            Assert.Equal("#7FFF0000", editor.Value);
            Assert.NotEmpty(AutomationProperties.GetHelpText(textBox));

            editor.CancelEdit();
            Assert.Equal("#7FFF0000", textBox.Text);
            Assert.Empty(AutomationProperties.GetHelpText(textBox));
        });
    }

    [Fact]
    public void SixDigitManualColourDefaultsToOpaqueOnSta()
    {
        RunOnSta(() =>
        {
            RgbaColorEditor editor = new();
            TextBox textBox = Assert.IsType<TextBox>(editor.FindName("ColorTextBox"));
            textBox.Text = "#123456";

            Assert.True(editor.Commit());
            Assert.Equal("#FF123456", editor.Value);
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
