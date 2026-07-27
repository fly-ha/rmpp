namespace Rmpp.Application.Validation;

/// <summary>表示可供设计器、预览和打印门禁共同消费的结构化问题。</summary>
public sealed record ValidationIssue(
    string Code,
    string Message,
    ValidationSeverity Severity,
    ValidationLocation Location)
{
    public bool BlocksOutput => Severity == ValidationSeverity.Error;
}
