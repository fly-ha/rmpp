using Rmpp.Domain.Printing;

namespace Rmpp.Printing.Windows.Calibration;

/// <summary>严格按稳定打印机 ID 与介质 key 查找本机档案，禁止跨介质误用。</summary>
public sealed class CalibrationProfileResolver
{
    public static CalibrationProfile? Resolve(
        IEnumerable<CalibrationProfile> profiles,
        string printerStableId,
        string mediaKey)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return profiles.SingleOrDefault(profile =>
            string.Equals(profile.Key.PrinterStableId, printerStableId, StringComparison.Ordinal)
            && string.Equals(profile.Key.MediaKey, mediaKey, StringComparison.Ordinal));
    }
}
