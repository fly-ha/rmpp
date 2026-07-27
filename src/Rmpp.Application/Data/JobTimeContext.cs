using System.Globalization;
using Rmpp.Domain.Data;

namespace Rmpp.Application.Data;

/// <summary>冻结一次预览或打印任务的参考时间，避免长任务跨午夜后值发生漂移。</summary>
public sealed record JobTimeContext
{
    public required DateTimeOffset ReferenceTime { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public DateTimeOffset Resolve(
        DateTimeDefinition definition,
        DateTimeOffset? pageTime = null,
        DateTimeOffset? previewTime = null) => definition.ValueMode switch
        {
            DateTimeValueMode.PerPage => pageTime ?? ReferenceTime,
            DateTimeValueMode.CurrentPreview => previewTime ?? ReferenceTime,
            _ => ReferenceTime,
        };
}
