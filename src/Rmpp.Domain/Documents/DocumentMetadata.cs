namespace Rmpp.Domain.Documents;

public sealed record DocumentMetadata
{
    public string Title { get; init; } = "Untitled";
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; init; } = DateTimeOffset.UtcNow;
}
