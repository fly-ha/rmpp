using Rmpp.Application.Editing.Clipboard;

namespace Rmpp.Application.Abstractions;

/// <summary>隔离系统剪贴板线程模型，应用层只处理安全的元素负载。</summary>
public interface IClipboardAdapter
{
    Task SetElementsAsync(
        ElementClipboardPayload payload,
        CancellationToken cancellationToken = default);

    Task<ElementClipboardPayload?> GetElementsAsync(CancellationToken cancellationToken = default);
}
