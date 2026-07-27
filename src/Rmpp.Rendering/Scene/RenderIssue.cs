namespace Rmpp.Rendering.Scene;

public enum RenderIssueSeverity
{
    Information,
    Warning,
    Error,
}

/// <summary>关联页面、元素或资源的结构化渲染问题。</summary>
public sealed record RenderIssue(
    string Code,
    string Message,
    RenderIssueSeverity Severity,
    Guid? ElementId = null,
    int? PageNumber = null);
