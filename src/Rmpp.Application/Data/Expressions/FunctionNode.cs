namespace Rmpp.Application.Data.Expressions;

/// <summary>调用固定白名单中的纯函数，函数名不会解析为 CLR 方法。</summary>
public sealed record FunctionNode(
    string FunctionName,
    IReadOnlyList<ExpressionNode> Arguments,
    ExpressionTextSpan Span) : ExpressionNode(Span);
