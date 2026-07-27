using SkiaSharp;

namespace Rmpp.Rendering.Text;

/// <summary>只从本机字体管理器解析字体，不下载、嵌入或查询网络字体。</summary>
public sealed class LocalFontCatalog
{
    private static readonly string[] PreferredFallbackFamilies =
    [
        "Microsoft YaHei",
        "Microsoft YaHei UI",
        "Noto Sans CJK SC",
        "Segoe UI",
        "Arial",
    ];

    private readonly SKFontManager fontManager;
    private readonly string[] families;
    private readonly Dictionary<string, string> familyLookup;

    public LocalFontCatalog(SKFontManager? fontManager = null)
    {
        this.fontManager = fontManager ?? SKFontManager.Default;
        families = this.fontManager.FontFamilies
            .Where(static family => !string.IsNullOrWhiteSpace(family))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static family => family, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (families.Length == 0)
        {
            throw new InvalidOperationException("No local fonts are available.");
        }

        familyLookup = families.ToDictionary(static family => family, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Families => families;

    public FontResolution Resolve(string requestedFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedFamily);
        if (familyLookup.TryGetValue(requestedFamily.Trim(), out string? exactFamily))
        {
            return new FontResolution(requestedFamily, exactFamily, false);
        }

        string fallback = PreferredFallbackFamilies
            .Select(candidate => familyLookup.GetValueOrDefault(candidate))
            .FirstOrDefault(static candidate => candidate is not null)
            ?? families[0];
        return new FontResolution(requestedFamily, fallback, true);
    }

    /// <summary>按粗体和斜体要求打开已解析字体；调用者负责释放返回对象。</summary>
    public SKTypeface OpenTypeface(FontResolution resolution, bool isBold, bool isItalic)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        SKFontStyle style = new(
            isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        return fontManager.MatchFamily(resolution.ResolvedFamily, style)
            ?? throw new InvalidOperationException($"Unable to open local font '{resolution.ResolvedFamily}'.");
    }
}
