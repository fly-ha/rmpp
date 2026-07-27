using System.Windows.Controls;

namespace Rmpp.Desktop.Controls;

/// <summary>为设计画布提供可复用的滚动视口。</summary>
public sealed class DesignerViewport : ScrollViewer
{
    public DesignerViewport()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        PanningMode = PanningMode.Both;
    }
}
