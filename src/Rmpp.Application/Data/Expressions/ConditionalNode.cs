namespace Rmpp.Application.Data.Expressions;

public sealed record ConditionalNode(
    ExpressionNode Condition,
    ExpressionNode WhenTrue,
    ExpressionNode WhenFalse,
    ExpressionTextSpan Span) : ExpressionNode(Span);
