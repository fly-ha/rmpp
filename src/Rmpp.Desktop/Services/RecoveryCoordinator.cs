using Rmpp.Desktop.Composition;
using System.IO;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Desktop.Services;

/// <summary>协调独立恢复包和 SQLite 索引；不接受或持久化导入数据行。</summary>
public sealed class RecoveryCoordinator(
    AppStoragePaths paths,
    RecoverySessionRepository repository,
    AtomicTemplateFileWriter writer,
    RmppPackageReader reader) : IDisposable
{
    private readonly Dictionary<Guid, Guid> sessionIds = [];
    private readonly Dictionary<Guid, PendingAutosave> pending = [];
    private readonly object gate = new();

    public event EventHandler<Exception>? AutosaveFailed;

    public Task<IReadOnlyList<RecoverySession>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        repository.GetAvailableAsync(cancellationToken);

    public async Task<RecoverySession> SaveAsync(
        TemplatePackageContent content,
        string? originalTemplatePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid sessionId = sessionIds.TryGetValue(content.Document.Id, out Guid existing) ? existing : Guid.NewGuid();
        sessionIds[content.Document.Id] = sessionId;
        string recoveryPath = Path.Combine(paths.RecoveryDirectory, $"{content.Document.Id:N}-{sessionId:N}.rmpp");
        await writer.WriteAsync(recoveryPath, content, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RecoverySession session = new()
        {
            SessionId = sessionId,
            DocumentId = content.Document.Id,
            OriginalTemplatePath = originalTemplatePath,
            RecoveryPackagePath = recoveryPath,
            CreatedAt = now,
            UpdatedAt = now,
            State = RecoverySessionState.Available,
        };
        await repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    public async Task<TemplatePackageContent> RestoreAsync(RecoverySession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using FileStream stream = new(session.RecoveryPackagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        TemplatePackageContent content = await reader.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        await repository.SetStateAsync(session.SessionId, RecoverySessionState.Restored, cancellationToken).ConfigureAwait(false);
        return content;
    }

    public async Task DiscardAsync(RecoverySession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await repository.SetStateAsync(session.SessionId, RecoverySessionState.Discarded, cancellationToken).ConfigureAwait(false);
        if (File.Exists(session.RecoveryPackagePath)) File.Delete(session.RecoveryPackagePath);
        sessionIds.Remove(session.DocumentId);
    }

    /// <summary>按文档去抖写入恢复包；仅接收模板包内容，不接收会话数据集。</summary>
    public void QueueAutosave(
        TemplatePackageContent content,
        string? originalTemplatePath,
        TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        TimeSpan delay = debounce ?? TimeSpan.FromSeconds(2);
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(debounce));
        PendingAutosave work;
        lock (gate)
        {
            if (pending.Remove(content.Document.Id, out PendingAutosave? previous))
            {
                previous.Cancellation.Cancel();
                previous.Cancellation.Dispose();
            }
            work = new PendingAutosave(content, originalTemplatePath, new CancellationTokenSource());
            pending[content.Document.Id] = work;
        }
        _ = RunAutosaveAsync(content.Document.Id, work, delay);
    }

    private async Task RunAutosaveAsync(Guid documentId, PendingAutosave work, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, work.Cancellation.Token).ConfigureAwait(false);
            await SaveAsync(work.Content, work.OriginalTemplatePath, work.Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            AutosaveFailed?.Invoke(this, exception);
        }
        finally
        {
            lock (gate)
            {
                if (pending.TryGetValue(documentId, out PendingAutosave? current) && ReferenceEquals(current, work)) pending.Remove(documentId);
            }
            work.Cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (PendingAutosave work in pending.Values) work.Cancellation.Cancel();
            pending.Clear();
        }
    }

    private sealed record PendingAutosave(
        TemplatePackageContent Content,
        string? OriginalTemplatePath,
        CancellationTokenSource Cancellation);
}
