namespace Rmpp.Application.Data.Expressions;

public sealed record LiteralNode(
    object? Value,
    ExpressionTextSpan Span) : ExpressionNode(Span);
