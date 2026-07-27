namespace Rmpp.Domain.Common;

internal static class DomainGuard
{
    public static double Finite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite.");
        }

        return value;
    }

    public static double NonNegative(double value, string parameterName)
    {
        Finite(value, parameterName);
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be non-negative.");
        }

        return value;
    }

    public static double InRange(double value, double minimum, double maximum, string parameterName)
    {
        Finite(value, parameterName);
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {minimum} and {maximum}.");
        }

        return value;
    }

    public static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
