using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Data;
using Rmpp.Application.Documents;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Desktop.ViewModels;

public sealed record PreviewFieldValue(string Name, string? Value);
public sealed record PreviewElementItem(Guid Id, string Name, string TypeName);

/// <summary>有数据时展示当前记录字段和值，无数据时退化为可联动画布选择的模板元素清单。</summary>
public sealed partial class DataPreviewViewModel : ObservableObject, IDisposable
{
    private readonly DataPreviewService previewService;
    private readonly DocumentSession? session;
    private TemplateDocument? document;

    public DataPreviewViewModel(DocumentSession? session = null, DataPreviewService? previewService = null)
    {
        this.session = session;
        this.previewService = previewService ?? new DataPreviewService();
        PreviousRowCommand = new RelayCommand(PreviousRow, () => CurrentIndex > 0);
        NextRowCommand = new RelayCommand(NextRow, () => DataSet is not null && CurrentIndex < DataSet.Count - 1);
        FindNextCommand = new RelayCommand(FindNext, () => DataSet is not null && !string.IsNullOrWhiteSpace(SearchText));
        ApplyRangeCommand = new RelayCommand(ApplyRange);
        if (session is not null)
        {
            document = session.State.Document;
            session.Changed += OnSessionChanged;
            RefreshElements();
        }
    }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(NextRowCommand)), NotifyCanExecuteChangedFor(nameof(PreviousRowCommand))]
    private int currentIndex;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(FindNextCommand))] private string searchText = string.Empty;
    [ObservableProperty] private DataSetSnapshot? dataSet;
    [ObservableProperty] private DataRowSnapshot? currentRow;
    [ObservableProperty] private ResolvedDataRecord? resolvedRecord;
    [ObservableProperty] private string statusText = "未导入数据，下面显示模板元素。";
    [ObservableProperty] private int rangeStart = 1;
    [ObservableProperty] private int rangeEnd = 1;
    [ObservableProperty] private PreviewElementItem? selectedElement;

    public ObservableCollection<DataColumnDefinition> Columns { get; } = [];
    public ObservableCollection<PreviewFieldValue> CurrentValues { get; } = [];
    public ObservableCollection<PreviewElementItem> Elements { get; } = [];
    public bool HasDataSource => DataSet is { Count: > 0 };
    public IRelayCommand PreviousRowCommand { get; }
    public IRelayCommand NextRowCommand { get; }
    public IRelayCommand FindNextCommand { get; }
    public IRelayCommand ApplyRangeCommand { get; }
    public int? SelectedRangeStart { get; private set; }
    public int? SelectedRangeEnd { get; private set; }

    partial void OnRangeStartChanged(int value) => OnPropertyChanged(nameof(SelectedRangeStart));
    partial void OnRangeEndChanged(int value) => OnPropertyChanged(nameof(SelectedRangeEnd));
    partial void OnSelectedElementChanged(PreviewElementItem? value)
    {
        if (value is not null) session?.SetSelection([value.Id]);
    }

    public void Load(DataSetSnapshot snapshot, TemplateDocument? document = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DataSet = snapshot;
        this.document = document ?? this.document;
        Columns.Clear();
        foreach (DataColumnDefinition column in snapshot.Schema.Columns) Columns.Add(column);
        CurrentIndex = 0;
        RangeStart = 1;
        RangeEnd = Math.Max(1, snapshot.Count);
        OnPropertyChanged(nameof(HasDataSource));
        UpdateCurrentRow();
    }

    public void AttachDocument(TemplateDocument? template)
    {
        document = template;
        RefreshElements();
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

    public void Dispose()
    {
        if (session is not null) session.Changed -= OnSessionChanged;
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
        CurrentValues.Clear();
        if (DataSet is null || DataSet.Count == 0)
        {
            CurrentRow = null;
            ResolvedRecord = null;
            StatusText = "未导入数据，下面显示模板元素。";
            OnPropertyChanged(nameof(HasDataSource));
            return;
        }
        CurrentIndex = Math.Clamp(CurrentIndex, 0, DataSet.Count - 1);
        CurrentRow = DataSet.Rows[CurrentIndex];
        foreach (DataColumnDefinition column in Columns)
        {
            CurrentValues.Add(new PreviewFieldValue(column.Name, CurrentRow.GetValue(column.Name)));
        }
        StatusText = $"第 {CurrentIndex + 1} / {DataSet.Count} 行";
        UpdateResolution();
        NextRowCommand.NotifyCanExecuteChanged();
        PreviousRowCommand.NotifyCanExecuteChanged();
    }

    private void UpdateResolution()
    {
        if (CurrentRow is null || document is null) { ResolvedRecord = null; return; }
        ResolvedRecord = previewService.Resolve(document, CurrentRow, new JobTimeContext { ReferenceTime = DateTimeOffset.Now });
    }

    private void OnSessionChanged(object? sender, DocumentSessionChangedEventArgs e)
    {
        document = e.State.Document;
        RefreshElements();
        UpdateResolution();
    }

    private void RefreshElements()
    {
        Elements.Clear();
        if (document is null) return;
        foreach (TemplateElement element in document.Elements.OrderBy(static element => element.ZIndex))
        {
            Elements.Add(new PreviewElementItem(element.Id, element.Name, element.GetType().Name));
        }
        SelectedElement = session?.State.SelectedElementIds.FirstOrDefault() is { } id && id != Guid.Empty
            ? Elements.FirstOrDefault(element => element.Id == id)
            : null;
    }
}
