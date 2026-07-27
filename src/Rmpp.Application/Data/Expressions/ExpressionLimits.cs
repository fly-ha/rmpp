namespace Rmpp.Application.Data.Expressions;

/// <summary>限制不可信表达式的文本、AST、递归、参数和输出规模。</summary>
public sealed record ExpressionLimits
{
    public int MaximumSourceCharacters { get; init; } = 4096;
    public int MaximumNodes { get; init; } = 256;
    public int MaximumDepth { get; init; } = 32;
    public int MaximumFunctionArguments { get; init; } = 32;
    public int MaximumOutputCharacters { get; init; } = 100_000;
}
