using CommunityToolkit.Mvvm.ComponentModel;
using Rmpp.Desktop.Resources;

namespace Rmpp.Desktop.ViewModels;

public sealed class StatusBarViewModel : ObservableObject
{
    private string text = DesktopText.Get("Ready");
    public string Text { get => text; set => SetProperty(ref text, value); }
}
