using Rmpp.Domain.Common;

namespace Rmpp.Domain.Printing;

public sealed record PrinterIdentity
{
    public PrinterIdentity(string stableId, string displayName)
    {
        StableId = DomainGuard.Required(stableId, nameof(stableId));
        DisplayName = DomainGuard.Required(displayName, nameof(displayName));
    }

    public string StableId { get; }
    public string DisplayName { get; }
}
