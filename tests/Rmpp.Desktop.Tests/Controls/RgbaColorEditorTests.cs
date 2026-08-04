using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Rmpp.Desktop.Controls;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
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

    [Fact]
    public void HexChannelsHsvAlphaAndExternalValuesUpdateImmediatelyOnSta()
    {
        RunOnSta(() =>
        {
            RgbaColorEditor editor = new() { Value = "#80402010" };
            TextBox outerHex = Assert.IsType<TextBox>(editor.FindName("ColorTextBox"));
            TextBox popupHex = Assert.IsType<TextBox>(editor.FindName("HexTextBox"));
            TextBox red = Assert.IsType<TextBox>(editor.FindName("RedTextBox"));
            TextBox green = Assert.IsType<TextBox>(editor.FindName("GreenTextBox"));
            TextBox blue = Assert.IsType<TextBox>(editor.FindName("BlueTextBox"));
            TextBox alpha = Assert.IsType<TextBox>(editor.FindName("AlphaTextBox"));

            popupHex.Text = "#7F112233";
            Assert.Equal("#7F112233", editor.Value);
            Assert.Equal("17", red.Text);
            Assert.Equal("34", green.Text);
            Assert.Equal("51", blue.Text);
            Assert.Equal("127", alpha.Text);

            red.Text = "1";
            green.Text = "2";
            blue.Text = "3";
            alpha.Text = "4";
            Assert.Equal("#04010203", editor.Value);
            Assert.Equal("#04010203", outerHex.Text);

            editor.ApplyHsv(120d, 1d, 1d);
            Assert.Equal("#0400FF00", editor.Value);
            editor.ApplyAlpha(0x80);
            Assert.Equal("#8000FF00", editor.Value);

            editor.Value = "#FF808080";
            editor.ApplyHsv(240d, 0d, 128d / 255d);
            editor.ApplyAlpha(0x40);
            Assert.Equal("240°", Assert.IsType<TextBlock>(editor.FindName("HueText")).Text);

            editor.Value = "#40102030";
            Assert.Equal("#40102030", outerHex.Text);
            Assert.Equal("#40102030", popupHex.Text);
            Assert.Equal("16", red.Text);
            Assert.Equal("32", green.Text);
            Assert.Equal("48", blue.Text);
            Assert.Equal("64", alpha.Text);
        });
    }

    [Fact]
    public void InvalidIntermediateTextDoesNotChangeValueAndCancelRestoresSessionStartOnSta()
    {
        RunOnSta(() =>
        {
            RgbaColorEditor editor = new() { Value = "#7F112233" };
            TextBox outerHex = Assert.IsType<TextBox>(editor.FindName("ColorTextBox"));
            TextBox alpha = Assert.IsType<TextBox>(editor.FindName("AlphaTextBox"));

            outerHex.Text = "#7F12";
            Assert.Equal("#7F12", outerHex.Text);
            Assert.Equal("#7F112233", editor.Value);

            alpha.Text = "256";
            Assert.Equal("256", alpha.Text);
            Assert.Equal("#7F112233", editor.Value);

            Assert.False(editor.Commit());
            Assert.Equal("#7F112233", outerHex.Text);
            Assert.NotEmpty(AutomationProperties.GetHelpText(outerHex));

            editor.BeginEditSession();
            editor.ApplyHsv(0d, 1d, 1d);
            editor.ApplyAlpha(0x40);
            Assert.Equal("#40FF0000", editor.Value);

            editor.CancelEdit();
            Assert.Equal("#7F112233", editor.Value);
            Assert.Equal("#7F112233", outerHex.Text);
            editor.EndEditSession();
        });
    }

    [Fact]
    public void LiveHexBindingUpdatesDocumentAndSharedRenderSceneWithoutCommitOnSta()
    {
        RunOnSta(() =>
        {
            TemplateDocument document = TemplateDocument.CreateNew("颜色实时渲染");
            RectangleElement rectangle = new()
            {
                LayerId = document.Layers[0].Id,
                Bounds = new MmRect(10, 10, 30, 20),
            };
            document = document with { Elements = [rectangle] };
            using DocumentTabViewModel tab = new(document);
            tab.Designer.Select(rectangle.Id, false);

            RgbaColorEditor editor = new();
            BindingOperations.SetBinding(editor, RgbaColorEditor.ValueProperty, new Binding(nameof(PropertiesViewModel.SolidFillColor))
            {
                Source = tab.Properties,
                Mode = BindingMode.TwoWay,
            });
            TextBox hex = Assert.IsType<TextBox>(editor.FindName("ColorTextBox"));

            hex.Text = "#80402010";

            RectangleElement updated = Assert.IsType<RectangleElement>(tab.Session.State.Document.Elements[0]);
            SolidFill fill = Assert.IsType<SolidFill>(updated.Fill);
            Assert.Equal(new RgbaColor(0x40, 0x20, 0x10, 0x80), fill.Color);
            RenderPathCommand command = Assert.IsType<RenderPathCommand>(
                new RenderSceneBuilder().Build(tab.Session.State.Document).Pages[0].Commands.Single(item => item.SourceId == rectangle.Id));
            Assert.Equal(fill.Color, Assert.IsType<RenderSolidFill>(command.Fill).Color);
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
