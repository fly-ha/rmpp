namespace Rmpp.Application.Data.Expressions;

/// <summary>表示表达式源文本中的零基位置和长度。</summary>
public readonly record struct ExpressionTextSpan(int Start, int Length)
{
    public int End => checked(Start + Length);
}

/// <summary>所有安全表达式节点的基类；AST 不保存可执行委托或 CLR 类型名。</summary>
public abstract record ExpressionNode(ExpressionTextSpan Span);

public enum ExpressionBinaryOperator
{
    Concatenate,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
}

public sealed record BinaryNode(
    ExpressionBinaryOperator Operator,
    ExpressionNode Left,
    ExpressionNode Right,
    ExpressionTextSpan Span) : ExpressionNode(Span);
