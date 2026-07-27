using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Desktop.ViewModels;

public sealed partial class TemplateCatalogViewModel(TemplateCatalogRepository repository, TemplateCatalogScanner scanner) : ObservableObject
{
    public ObservableCollection<TemplateCatalogEntry> Entries { get; } = [];
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);
    [ObservableProperty] private string statusText = string.Empty;

    public async Task RefreshAsync()
    {
        Entries.Clear();
        foreach (TemplateCatalogEntry entry in await repository.GetAllAsync().ConfigureAwait(true)) Entries.Add(entry);
        StatusText = $"模板目录包含 {Entries.Count} 项。";
    }

    public async Task ScanAsync(IEnumerable<string> directories, CancellationToken cancellationToken = default)
    {
        TemplateScanResult result = await scanner.ScanAsync(directories, cancellationToken).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusText = $"扫描 {result.ScannedFiles} 个文件，识别 {result.ValidTemplates} 个模板，{result.Issues.Count} 个问题。";
    }
}
