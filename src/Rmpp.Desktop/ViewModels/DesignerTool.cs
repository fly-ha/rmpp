using Rmpp.Desktop.Resources;

namespace Rmpp.Desktop.ViewModels;

/// <summary>定义设计器首版可创建的工具类型；选择工具只操作现有元素，不创建文档内容。</summary>
public enum DesignerTool
{
    Select,
    Text,
    DateTime,
    Serial,
    Image,
    Barcode,
    QrCode,
    DataMatrix,
    Line,
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Arc,
    Sector,
    Polyline,
    Polygon,
}

/// <summary>为工具箱提供稳定的工具标识和本地化显示名称。</summary>
public sealed record DesignerToolItem(DesignerTool Tool, string ResourceKey)
{
    public string DisplayName => DesktopText.Get(ResourceKey);
}
