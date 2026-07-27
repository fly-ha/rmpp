using System.Globalization;

namespace Rmpp.Application.Data.Expressions;

/// <summary>提供只读字段和显式文化；求值器无法访问文件、环境或任意服务。</summary>
public sealed record ExpressionEvaluationContext
{
    public IReadOnlyDictionary<string, string?> Fields { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}
