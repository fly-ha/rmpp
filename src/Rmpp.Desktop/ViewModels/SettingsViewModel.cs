using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Rmpp.Desktop.Composition;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Desktop.ViewModels;

/// <summary>读取和保存经过类型/版本验证的本地设置，只记录路径而不记录导入行。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly SettingDefinition<string> LanguageSetting = new("ui.language", "zh-CN", 1, static value => !string.IsNullOrWhiteSpace(value));
    private static readonly SettingDefinition<int> RecentLimitSetting = new("recent.maximumEntries", 20, 1, static value => value is >= 1 and <= 100);
    private static readonly SettingDefinition<string[]> CatalogRootsSetting = new("catalog.roots", Array.Empty<string>(), 1, static values => values.All(static value => !string.IsNullOrWhiteSpace(value)));
    private readonly SettingsRepository repository;

    public SettingsViewModel(SettingsRepository repository, AppStoragePaths paths)
    {
        this.repository = repository;
        StorageModeText = paths.IsPortable ? "便携模式" : "安装模式";
        DataDirectory = paths.Root;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    [ObservableProperty] private string language = "zh-CN";
    [ObservableProperty] private int recentMaximumEntries = 20;
    [ObservableProperty] private string catalogDirectoriesText = string.Empty;
    [ObservableProperty] private string statusText = string.Empty;
    public string StorageModeText { get; }
    public string DataDirectory { get; }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    public async Task LoadAsync()
    {
        SettingReadResult<string> language = await repository.GetAsync(LanguageSetting).ConfigureAwait(true);
        SettingReadResult<int> recent = await repository.GetAsync(RecentLimitSetting).ConfigureAwait(true);
        SettingReadResult<string[]> roots = await repository.GetAsync(CatalogRootsSetting).ConfigureAwait(true);
        Language = language.Value;
        RecentMaximumEntries = recent.Value;
        CatalogDirectoriesText = string.Join(Environment.NewLine, roots.Value);
        StatusText = new[] { language.Issue, recent.Issue, roots.Issue }.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "设置已加载。";
    }

    public async Task SaveAsync()
    {
        string[] roots = CatalogDirectoriesText.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await repository.SetAsync(LanguageSetting, Language).ConfigureAwait(true);
        await repository.SetAsync(RecentLimitSetting, RecentMaximumEntries).ConfigureAwait(true);
        await repository.SetAsync(CatalogRootsSetting, roots).ConfigureAwait(true);
        StatusText = "设置已保存到本地 SQLite。";
    }

    public static SettingDefinition<string[]> CatalogRoots => CatalogRootsSetting;
}
