using System.Windows;
using Microsoft.Win32;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class DataImportDialog : Window
{
    public DataImportDialog() { InitializeComponent(); }

    private void OnChooseFile(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "数据文件|*.csv;*.xlsx|CSV|*.csv|Excel|*.xlsx|所有文件|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true && DataContext is DataImportViewModel viewModel)
        {
            viewModel.SetFilePath(dialog.FileName);
        }
    }
}
