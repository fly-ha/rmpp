using System.Globalization;

namespace Rmpp.Application.Data.Expressions;

/// <summary>固定、不可扩展的纯函数白名单，杜绝反射和任意方法调用。</summary>
public sealed class ExpressionFunctionRegistry
{
    private static readonly HashSet<string> AllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "concat", "default", "if", "eq", "ne", "gt", "gte", "lt", "lte",
        "substring", "padleft", "padright", "upper", "lower", "formatnumber", "formatdate",
    };

    public static bool IsAllowed(string name) => AllowedNames.Contains(name);

    public static object? Invoke(
        string name,
        IReadOnlyList<object?> arguments,
        ExpressionEvaluationContext context,
        ExpressionTextSpan span)
    {
        try
        {
            return name.ToLowerInvariant() switch
            {
                "concat" => string.Concat(arguments.Select(ToText)),
                "default" => Default(arguments),
                "if" => If(arguments),
                "eq" => Compare(arguments, static result => result == 0),
                "ne" => Compare(arguments, static result => result != 0),
                "gt" => Compare(arguments, static result => result > 0),
                "gte" => Compare(arguments, static result => result >= 0),
                "lt" => Compare(arguments, static result => result < 0),
                "lte" => Compare(arguments, static result => result <= 0),
                "substring" => Substring(arguments),
                "padleft" => Pad(arguments, left: true),
                "padright" => Pad(arguments, left: false),
                "upper" => UnaryText(arguments).ToUpper(context.Culture),
                "lower" => UnaryText(arguments).ToLower(context.Culture),
                "formatnumber" => FormatNumber(arguments, context.Culture),
                "formatdate" => FormatDate(arguments, context.Culture),
                _ => throw new InvalidOperationException($"Function '{name}' is not allowed."),
            };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new ExpressionEvaluationException(exception.Message, span, exception);
        }
    }

    private static object? Default(IReadOnlyList<object?> arguments)
    {
        RequireCount(arguments, 2);
        return arguments[0] is null || arguments[0] is string text && text.Length == 0
            ? arguments[1]
            : arguments[0];
    }

    private static object? If(IReadOnlyList<object?> arguments)
    {
        RequireCount(arguments, 3);
        return ExpressionEvaluator.ToBoolean(arguments[0]) ? arguments[1] : arguments[2];
    }

    private static bool Compare(IReadOnlyList<object?> arguments, Func<int, bool> predicate)
    {
        RequireCount(arguments, 2);
        return predicate(ExpressionEvaluator.Compare(arguments[0], arguments[1]));
    }

    private static string Substring(IReadOnlyList<object?> arguments)
    {
        if (arguments.Count is < 2 or > 3)
        {
            throw new ArgumentException("substring requires value, start, and optional length.");
        }

        string value = ToText(arguments[0]);
        int start = ToInt(arguments[1]);
        if (start < 0 || start > value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "Substring start is outside the value.");
        }

        return arguments.Count == 2
            ? value[start..]
            : value.Substring(start, Math.Clamp(ToInt(arguments[2]), 0, value.Length - start));
    }

    private static string Pad(IReadOnlyList<object?> arguments, bool left)
    {
        if (arguments.Count is < 2 or > 3)
        {
            throw new ArgumentException("Padding requires value, width, and optional character.");
        }

        string value = ToText(arguments[0]);
        int width = ToInt(arguments[1]);
        char padding = arguments.Count == 3 && ToText(arguments[2]) is { Length: > 0 } text ? text[0] : ' ';
        return left ? value.PadLeft(width, padding) : value.PadRight(width, padding);
    }

    private static string UnaryText(IReadOnlyList<object?> arguments)
    {
        RequireCount(arguments, 1);
        return ToText(arguments[0]);
    }

    private static string FormatNumber(IReadOnlyList<object?> arguments, CultureInfo culture)
    {
        RequireCount(arguments, 2);
        decimal value = Convert.ToDecimal(arguments[0], culture);
        return value.ToString(ToText(arguments[1]), culture);
    }

    private static string FormatDate(IReadOnlyList<object?> arguments, CultureInfo culture)
    {
        RequireCount(arguments, 2);
        DateTimeOffset value = arguments[0] switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(dateTime),
            _ => DateTimeOffset.Parse(ToText(arguments[0]), culture, DateTimeStyles.AllowWhiteSpaces),
        };
        return value.ToString(ToText(arguments[1]), culture);
    }

    private static int ToInt(object? value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);

    internal static string ToText(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static void RequireCount(IReadOnlyList<object?> arguments, int count)
    {
        if (arguments.Count != count)
        {
            throw new ArgumentException($"Function requires exactly {count} argument(s).");
        }
    }
}
