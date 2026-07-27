using System.Diagnostics;
using System.IO;

namespace Rmpp.Desktop.Services;

public sealed record LocalHelpTopic(string Id, string Title, string Path);

/// <summary>只读取随应用分发的本地 Markdown；外部链接必须由用户显式触发并交给操作系统处理。</summary>
public sealed class LocalHelpService
{
    private readonly string helpRoot;

    public LocalHelpService(string? helpRoot = null)
    {
        this.helpRoot = Path.GetFullPath(helpRoot ?? Path.Combine(AppContext.BaseDirectory, "help"));
    }

    public IReadOnlyList<LocalHelpTopic> GetTopics()
    {
        if (!Directory.Exists(helpRoot)) return Array.Empty<LocalHelpTopic>();
        return Directory.EnumerateFiles(helpRoot, "*.md", SearchOption.TopDirectoryOnly)
            .Select(path => new LocalHelpTopic(Path.GetFileNameWithoutExtension(path), ReadTitle(path), path))
            .OrderBy(static topic => topic.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public string ReadTopic(string topicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
        string path = Path.GetFullPath(Path.Combine(helpRoot, topicId + ".md"));
        if (!IsUnderHelpRoot(path) || !File.Exists(path)) throw new FileNotFoundException("未找到本地帮助主题。", path);
        return File.ReadAllText(path);
    }

    public void OpenTopicWithSystemHandler(string topicId)
    {
        LocalHelpTopic topic = GetTopics().FirstOrDefault(item => string.Equals(item.Id, topicId, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("未找到本地帮助主题。");
        Process.Start(new ProcessStartInfo(topic.Path) { UseShellExecute = true });
    }

    public static void OpenExternalLink(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https")) throw new ArgumentException("只允许用户主动打开 HTTP/HTTPS 外部链接。", nameof(uri));
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private bool IsUnderHelpRoot(string path)
    {
        string relative = Path.GetRelativePath(helpRoot, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string ReadTitle(string path)
    {
        string? first = File.ReadLines(path).FirstOrDefault();
        return first?.TrimStart('#', ' ') is { Length: > 0 } title ? title : Path.GetFileNameWithoutExtension(path);
    }
}
