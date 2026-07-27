using System.Globalization;
using System.Windows;

namespace Rmpp.Desktop.Resources;

/// <summary>从本地 ResourceDictionary 读取用户文本；测试宿主没有 Application 时返回稳定资源键。</summary>
public static class DesktopText
{
    public static string Get(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), values);
}
