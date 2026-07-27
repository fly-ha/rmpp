namespace Rmpp.Domain.Styles;

public abstract record FillStyle
{
    public static FillStyle None { get; } = new NoFill();
}

public sealed record NoFill : FillStyle;
