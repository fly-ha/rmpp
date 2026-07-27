using System.Globalization;

namespace Rmpp.Application.Data.Expressions;

/// <summary>递归求值固定 AST，并在每个字符串边界执行输出上限。</summary>
public sealed class ExpressionEvaluator(ExpressionLimits? limits = null)
{
    private readonly ExpressionLimits limits = limits ?? new ExpressionLimits();

    public object? Evaluate(ExpressionNode node, ExpressionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);
        return EnforceOutput(EvaluateNode(node, context), node.Span);
    }

    public string EvaluateText(ExpressionNode node, ExpressionEvaluationContext context) =>
        ExpressionFunctionRegistry.ToText(Evaluate(node, context));

    private object? EvaluateNode(ExpressionNode node, ExpressionEvaluationContext context) => node switch
    {
        LiteralNode literal => literal.Value,
        FieldNode field => context.Fields.TryGetValue(field.FieldName, out string? value) ? value : null,
        BinaryNode binary => EvaluateBinary(binary, context),
        ConditionalNode conditional => ToBoolean(EvaluateNode(conditional.Condition, context))
            ? EvaluateNode(conditional.WhenTrue, context)
            : EvaluateNode(conditional.WhenFalse, context),
        FunctionNode function => ExpressionFunctionRegistry.Invoke(
            function.FunctionName,
            function.Arguments.Select(argument => EvaluateNode(argument, context)).ToArray(),
            context,
            function.Span),
        _ => throw new ExpressionEvaluationException("Expression node is not supported.", node.Span),
    };

    private object EvaluateBinary(BinaryNode binary, ExpressionEvaluationContext context)
    {
        object? left = EvaluateNode(binary.Left, context);
        object? right = EvaluateNode(binary.Right, context);
        return binary.Operator switch
        {
            ExpressionBinaryOperator.Concatenate => ExpressionFunctionRegistry.ToText(left) + ExpressionFunctionRegistry.ToText(right),
            ExpressionBinaryOperator.Equal => Compare(left, right) == 0,
            ExpressionBinaryOperator.NotEqual => Compare(left, right) != 0,
            ExpressionBinaryOperator.GreaterThan => Compare(left, right) > 0,
            ExpressionBinaryOperator.GreaterThanOrEqual => Compare(left, right) >= 0,
            ExpressionBinaryOperator.LessThan => Compare(left, right) < 0,
            ExpressionBinaryOperator.LessThanOrEqual => Compare(left, right) <= 0,
            _ => throw new ExpressionEvaluationException("Binary operator is not supported.", binary.Span),
        };
    }

    private object? EnforceOutput(object? value, ExpressionTextSpan span)
    {
        if (value is string text && text.Length > limits.MaximumOutputCharacters)
        {
            throw new ExpressionEvaluationException(
                $"Expression output exceeds the configured {limits.MaximumOutputCharacters} character limit.",
                span);
        }

        return value;
    }

    internal static bool ToBoolean(object? value) => value switch
    {
        null => false,
        bool boolean => boolean,
        string text when bool.TryParse(text, out bool boolean) => boolean,
        string text => text.Length > 0,
        decimal number => number != 0,
        int number => number != 0,
        long number => number != 0,
        _ => true,
    };

    internal static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null ? 0 : left is null ? -1 : 1;
        }

        if (TryDecimal(left, out decimal leftNumber) && TryDecimal(right, out decimal rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean.CompareTo(rightBoolean);
        }

        return StringComparer.Ordinal.Compare(
            ExpressionFunctionRegistry.ToText(left),
            ExpressionFunctionRegistry.ToText(right));
    }

    private static bool TryDecimal(object value, out decimal number)
    {
        if (value is decimal direct)
        {
            number = direct;
            return true;
        }

        return decimal.TryParse(
            ExpressionFunctionRegistry.ToText(value),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out number);
    }
}
