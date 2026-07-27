using System.Windows;
using Rmpp.Desktop.ViewModels;

namespace Rmpp.Desktop.Views;

public partial class RecoveryDialog : Window
{
    public RecoveryDialog() { InitializeComponent(); Loaded += OnLoaded; }
    private async void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is RecoveryDialogViewModel vm) await vm.RefreshAsync(); }
    private async void OnRestore(object sender, RoutedEventArgs e) { if (DataContext is RecoveryDialogViewModel vm) { await vm.RestoreSelectedAsync(); DialogResult = true; } }
    private async void OnDiscard(object sender, RoutedEventArgs e) { if (DataContext is RecoveryDialogViewModel vm) await vm.DiscardSelectedAsync(); }
}
