namespace Rmpp.Rendering.Text;

/// <summary>记录模板请求字体与实际使用的本机字体，确保缺失字体替换可见且可重复。</summary>
public sealed record FontResolution(
    string RequestedFamily,
    string ResolvedFamily,
    bool UsedFallback);
