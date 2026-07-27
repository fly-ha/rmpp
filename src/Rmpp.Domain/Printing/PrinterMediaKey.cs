using Rmpp.Domain.Common;

namespace Rmpp.Domain.Printing;

public sealed record PrinterMediaKey
{
    public PrinterMediaKey(string printerStableId, string mediaKey)
    {
        PrinterStableId = DomainGuard.Required(printerStableId, nameof(printerStableId));
        MediaKey = DomainGuard.Required(mediaKey, nameof(mediaKey));
    }

    public string PrinterStableId { get; }
    public string MediaKey { get; }
}
