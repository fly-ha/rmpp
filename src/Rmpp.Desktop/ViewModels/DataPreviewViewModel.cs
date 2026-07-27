using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Data;
using Rmpp.Domain.Documents;

namespace Rmpp.Desktop.ViewModels;

/// <summary>展示会话数据的字段、行导航、搜索和最终解析结果，不持久化行内容。</summary>
public sealed partial class DataPreviewViewModel : ObservableObject
{
    private readonly DataPreviewService previewService;
    private TemplateDocument? document;

    public DataPreviewViewModel(DataPreviewService? previewService = null)
    {
        this.previewService = previewService ?? new DataPreviewService();
        PreviousRowCommand = new RelayCommand(PreviousRow, () => CurrentIndex > 0);
        NextRowCommand = new RelayCommand(NextRow, () => DataSet is not null && CurrentIndex < DataSet.Count - 1);
        FindNextCommand = new RelayCommand(FindNext, () => DataSet is not null && !string.IsNullOrWhiteSpace(SearchText));
        ApplyRangeCommand = new RelayCommand(ApplyRange);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextRowCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousRowCommand))]
    private int currentIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand))]
    private string searchText = string.Empty;

    [ObservableProperty]
    private DataSetSnapshot? dataSet;

    [ObservableProperty]
    private DataRowSnapshot? currentRow;

    [ObservableProperty]
    private ResolvedDataRecord? resolvedRecord;

    [ObservableProperty]
    private string statusText = string.Empty;

    public ObservableCollection<DataColumnDefinition> Columns { get; } = [];
    public IRelayCommand PreviousRowCommand { get; }
    public IRelayCommand NextRowCommand { get; }
    public IRelayCommand FindNextCommand { get; }
    public IRelayCommand ApplyRangeCommand { get; }

    [ObservableProperty] private int rangeStart = 1;
    [ObservableProperty] private int rangeEnd = 1;
    public int? SelectedRangeStart { get; private set; }
    public int? SelectedRangeEnd { get; private set; }

    partial void OnRangeStartChanged(int value) => OnPropertyChanged(nameof(SelectedRangeStart));
    partial void OnRangeEndChanged(int value) => OnPropertyChanged(nameof(SelectedRangeEnd));

    public void Load(DataSetSnapshot snapshot, TemplateDocument? document = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DataSet = snapshot;
        this.document = document;
        Columns.Clear();
        foreach (DataColumnDefinition column in snapshot.Schema.Columns)
        {
            Columns.Add(column);
        }

        CurrentIndex = 0;
        RangeStart = 1;
        RangeEnd = Math.Max(1, snapshot.Count);
        UpdateCurrentRow();
    }

    public void AttachDocument(TemplateDocument? template)
    {
        document = template;
        UpdateResolution();
    }

    public void SelectRange(int start, int end)
    {
        if (DataSet is null)
        {
            SelectedRangeStart = SelectedRangeEnd = null;
            return;
        }

        SelectedRangeStart = Math.Clamp(Math.Min(start, end), 0, Math.Max(0, DataSet.Count - 1));
        SelectedRangeEnd = Math.Clamp(Math.Max(start, end), 0, Math.Max(0, DataSet.Count - 1));
        OnPropertyChanged(nameof(SelectedRangeStart));
        OnPropertyChanged(nameof(SelectedRangeEnd));
    }

    private void ApplyRange()
    {
        if (DataSet is null || DataSet.Count == 0) return;
        SelectRange(RangeStart - 1, RangeEnd - 1);
        StatusText = $"已选择第 {SelectedRangeStart!.Value + 1} 至 {SelectedRangeEnd!.Value + 1} 行。";
    }

    private void PreviousRow() { CurrentIndex--; UpdateCurrentRow(); }
    private void NextRow() { CurrentIndex++; UpdateCurrentRow(); }

    private void FindNext()
    {
        if (DataSet is null || string.IsNullOrWhiteSpace(SearchText)) return;
        int found = DataPreviewService.FindNext(DataSet, SearchText, Math.Min(CurrentIndex + 1, DataSet.Count));
        if (found >= 0) { CurrentIndex = found; UpdateCurrentRow(); }
        else StatusText = "未找到匹配记录。";
    }

    private void UpdateCurrentRow()
    {
        if (DataSet is null || DataSet.Count == 0)
        {
            CurrentRow = null;
            ResolvedRecord = null;
            StatusText = "没有可预览的记录。";
            return;
        }

        CurrentIndex = Math.Clamp(CurrentIndex, 0, DataSet.Count - 1);
        CurrentRow = DataSet.Rows[CurrentIndex];
        StatusText = $"第 {CurrentIndex + 1} / {DataSet.Count} 行";
        UpdateResolution();
        NextRowCommand.NotifyCanExecuteChanged();
        PreviousRowCommand.NotifyCanExecuteChanged();
    }

    private void UpdateResolution()
    {
        if (CurrentRow is null || document is null) { ResolvedRecord = null; return; }
        ResolvedRecord = previewService.Resolve(document, CurrentRow, new JobTimeContext
        {
            ReferenceTime = DateTimeOffset.Now,
        });
    }
}
