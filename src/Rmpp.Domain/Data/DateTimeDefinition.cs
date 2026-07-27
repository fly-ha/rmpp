namespace Rmpp.Domain.Data;

public enum DateTimeValueMode { JobStart, PerPage, CurrentPreview }

public sealed record DateTimeDefinition
{
    public string Format { get; init; } = "yyyy-MM-dd HH:mm:ss";
    public string? CultureName { get; init; }
    public DateTimeValueMode ValueMode { get; init; } = DateTimeValueMode.JobStart;
}
