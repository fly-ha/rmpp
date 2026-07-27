using Rmpp.Desktop.Resources;

namespace Rmpp.Desktop.ViewModels;

public sealed record StartPageViewModel
{
    public string Title { get; init; } = DesktopText.Get("StartTitle");
    public string Description { get; init; } = DesktopText.Get("StartDescription");
}
