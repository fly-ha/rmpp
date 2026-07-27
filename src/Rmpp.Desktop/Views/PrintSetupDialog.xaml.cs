using System.Windows;
using Microsoft.Win32;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class PrintSetupDialog : Window
{
    public PrintSetupDialog() { InitializeComponent(); }

    private void OnChoosePdf(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new() { Filter = "PDF 文件|*.pdf", AddExtension = true, DefaultExt = ".pdf" };
        if (dialog.ShowDialog(this) == true && DataContext is PrintSetupViewModel { PdfExport: not null } viewModel)
        {
            viewModel.PdfExport.OutputPath = dialog.FileName;
        }
    }
}
