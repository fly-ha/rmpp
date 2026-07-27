using Rmpp.Domain.Printing;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Printing.Windows.Jobs;

namespace Rmpp.Desktop.Services;

/// <summary>在桌面组合层连接 SQLite 校准仓储与 Windows 打印端口，保持底层模块依赖单向。</summary>
public sealed class CalibrationProfileProviderAdapter(CalibrationProfileRepository repository) : ICalibrationProfileProvider
{
    public async ValueTask<CalibrationProfile?> GetAsync(PrinterMediaKey key, CancellationToken cancellationToken = default) =>
        await repository.GetAsync(key, cancellationToken).ConfigureAwait(false);
}
