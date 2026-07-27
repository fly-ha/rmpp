namespace Rmpp.Domain.Data;

public sealed record ElementExpression(string Source)
{
    public static ElementExpression Literal(string value) => new($"{value}");
}
