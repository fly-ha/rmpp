using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Desktop.Services;
using Rmpp.Infrastructure.Persistence.Models;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Desktop.ViewModels;

public sealed partial class RecoveryDialogViewModel(RecoveryCoordinator coordinator) : ObservableObject
{
    public ObservableCollection<RecoverySession> Sessions { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);

    [ObservableProperty] private RecoverySession? selectedSession;
    [ObservableProperty] private string statusText = string.Empty;

    public event EventHandler<TemplatePackageContent>? Restored;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Sessions.Clear();
        foreach (RecoverySession session in await coordinator.GetAvailableAsync(cancellationToken).ConfigureAwait(true)) Sessions.Add(session);
        SelectedSession = Sessions.FirstOrDefault();
        StatusText = Sessions.Count == 0 ? "没有可恢复的文档。" : $"发现 {Sessions.Count} 个恢复版本。";
    }

    public async Task RestoreSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSession is null) return;
        TemplatePackageContent content = await coordinator.RestoreAsync(SelectedSession, cancellationToken).ConfigureAwait(true);
        Restored?.Invoke(this, content);
        StatusText = "恢复版本已打开。";
    }

    public async Task DiscardSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSession is null) return;
        await coordinator.DiscardAsync(SelectedSession, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }
}
