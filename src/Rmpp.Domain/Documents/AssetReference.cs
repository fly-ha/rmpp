using Rmpp.Domain.Common;

namespace Rmpp.Domain.Documents;

public sealed record AssetReference
{
    public AssetReference(Guid id, string fileName, string mediaType, string sha256)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Asset id cannot be empty.", nameof(id)) : id;
        FileName = DomainGuard.Required(fileName, nameof(fileName));
        MediaType = DomainGuard.Required(mediaType, nameof(mediaType));
        Sha256 = DomainGuard.Required(sha256, nameof(sha256));
    }

    public Guid Id { get; }
    public string FileName { get; }
    public string MediaType { get; }
    public string Sha256 { get; }
}
