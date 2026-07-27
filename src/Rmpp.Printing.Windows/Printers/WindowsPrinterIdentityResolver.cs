using System.Security.Cryptography;
using System.Text;
using Rmpp.Domain.Printing;

namespace Rmpp.Printing.Windows.Printers;

/// <summary>使用服务器、队列、驱动和端口生成与显示名称分离的稳定打印机身份。</summary>
public sealed class WindowsPrinterIdentityResolver
{
    public static PrinterIdentity Resolve(WindowsPrintQueueSnapshot queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        string material = string.Join('\n',
            Normalize(queue.ServerName),
            Normalize(queue.QueueName),
            Normalize(queue.DriverName),
            Normalize(queue.PortName));
        string stableId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        return new PrinterIdentity(stableId, queue.DisplayName);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
}
