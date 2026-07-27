using System.Windows;
using System.Windows.Controls;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class ExpressionEditorView : UserControl
{
    public ExpressionEditorView() { InitializeComponent(); }

    private void OnDropField(object sender, DragEventArgs e)
    {
        if (DataContext is ExpressionEditorViewModel viewModel && e.Data.GetData(DataFormats.StringFormat) is string value)
        {
            viewModel.InsertFieldCommand.Execute(value.Trim('[', ']'));
            e.Handled = true;
        }
    }

    private void OnFieldSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: string field } && DataContext is ExpressionEditorViewModel viewModel)
        {
            viewModel.InsertFieldCommand.Execute(field);
        }
    }

    private void OnFunctionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: string function } && DataContext is ExpressionEditorViewModel viewModel)
        {
            viewModel.Source += (viewModel.Source.Length == 0 ? string.Empty : " + ") + function + "()";
            viewModel.ValidateCommand.Execute(null);
        }
    }
}
