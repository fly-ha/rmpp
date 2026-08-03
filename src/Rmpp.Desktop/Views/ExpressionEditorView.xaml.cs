using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class ExpressionEditorView : UserControl
{
    public ExpressionEditorView() { InitializeComponent(); }

    private void OnDropField(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.StringFormat) is string value)
        {
            InsertAtCaret(value);
            e.Handled = true;
        }
    }

    private void OnFieldActivated(object sender, MouseButtonEventArgs e) => InsertSelectedField();
    private void OnFunctionActivated(object sender, MouseButtonEventArgs e) => InsertSelectedFunction();
    private void OnFieldKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { InsertSelectedField(); e.Handled = true; } }
    private void OnFunctionKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { InsertSelectedFunction(); e.Handled = true; } }

    private void InsertSelectedField()
    {
        if (ExpressionFieldsList.SelectedItem is string field) InsertAtCaret($"[{field}]");
    }

    private void InsertSelectedFunction()
    {
        if (ExpressionFunctionsList.SelectedItem is string function) InsertAtCaret(function + "()");
    }

    private void InsertAtCaret(string text)
    {
        int caret = Editor.CaretIndex;
        string source = Editor.Text ?? string.Empty;
        Editor.Text = source.Insert(caret, text);
        Editor.CaretIndex = caret + text.Length;
        Editor.Focus();
        if (DataContext is ExpressionEditorViewModel viewModel) viewModel.ValidateCommand.Execute(null);
    }
}
