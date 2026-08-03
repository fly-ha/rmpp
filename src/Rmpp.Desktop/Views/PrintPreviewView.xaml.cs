using System.Windows.Controls;
using System.Windows;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class PrintPreviewView : UserControl
{
    public PrintPreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadPreviewAsync();

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded) await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        if (DataContext is PrintPreviewViewModel viewModel)
        {
            await viewModel.LoadPageAsync();
        }
    }
}
