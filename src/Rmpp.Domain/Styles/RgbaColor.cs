namespace Rmpp.Domain.Styles;

public readonly record struct RgbaColor(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue)
{
    public static RgbaColor Transparent => new(0, 0, 0, 0);
    public static RgbaColor Black => new(0, 0, 0);
    public static RgbaColor White => new(255, 255, 255);
}
