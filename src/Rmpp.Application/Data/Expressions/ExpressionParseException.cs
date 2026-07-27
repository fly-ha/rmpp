namespace Rmpp.Application.Data.Expressions;

/// <summary>表示带源位置的安全表达式语法或复杂度错误。</summary>
public sealed class ExpressionParseException : Exception
{
    public ExpressionParseException(string message, ExpressionTextSpan span)
        : base(message)
    {
        Span = span;
    }

    public ExpressionTextSpan Span { get; }
}

public sealed class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string message, ExpressionTextSpan span, Exception? innerException = null)
        : base(message, innerException)
    {
        Span = span;
    }

    public ExpressionTextSpan Span { get; }
}
