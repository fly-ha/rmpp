using System.IO;
using System.Windows.Media.Imaging;
using Rmpp.Rendering.Scene;

namespace Rmpp.Printing.Windows.Rendering;

/// <summary>把场景图片引用解析为冻结的 WPF 位图；模板包资产由桌面组合根提供实现。</summary>
public interface IPrintImageResolver
{
    BitmapSource? Resolve(RenderImage image);
}

/// <summary>仅解析显式本地路径的默认离线实现，不访问网络 URI。</summary>
public sealed class LocalPrintImageResolver : IPrintImageResolver
{
    public BitmapSource? Resolve(RenderImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (string.IsNullOrWhiteSpace(image.LocalPath) || !File.Exists(image.LocalPath))
        {
            return null;
        }

        Uri uri = new(Path.GetFullPath(image.LocalPath), UriKind.Absolute);
        if (!uri.IsFile)
        {
            return null;
        }

        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = uri;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
