using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;

namespace Rmpp.Desktop.ViewModels;

/// <summary>管理一次会话内的数据源导入；行值只保存在当前视图模型和预览集合中。</summary>
public sealed partial class DataImportViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<IDataSourceReader> readers;
    private CancellationTokenSource? importCancellation;

    public DataImportViewModel(IEnumerable<IDataSourceReader>? readers = null)
    {
        this.readers = (readers ?? Array.Empty<IDataSourceReader>()).ToArray();
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private string filePath = string.Empty;

    [ObservableProperty]
    private bool hasHeaderRow = true;

    [ObservableProperty]
    private string encodingName = "utf-8";

    [ObservableProperty]
    private string delimiter = ",";

    [ObservableProperty]
    private string quote = "\"";

    [ObservableProperty]
    private string cultureName = string.Empty;

    [ObservableProperty]
    private string worksheetName = string.Empty;

    [ObservableProperty]
    private string cellRange = string.Empty;

    [ObservableProperty]
    private int maximumRows = 100_000;

    [ObservableProperty]
    private int previewRowLimit = 200;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string errorText = string.Empty;

    [ObservableProperty]
    private DataSetSnapshot? dataSet;

    public ObservableCollection<DataColumnDefinition> Columns { get; } = [];
    public ObservableCollection<DataRowSnapshot> PreviewRows { get; } = [];
    public ObservableCollection<string> PreviewLines { get; } = [];
    public IAsyncRelayCommand ImportCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public event EventHandler<DataSetSnapshot>? Imported;

    public bool CanImport() => !IsBusy && File.Exists(FilePath) && readers.Any(reader => reader.CanRead(Path.GetExtension(FilePath)));

    public void SetFilePath(string path)
    {
        FilePath = path ?? string.Empty;
        ErrorText = string.Empty;
        StatusText = string.Empty;
        ImportCommand.NotifyCanExecuteChanged();
    }

    private async Task ImportAsync()
    {
        if (!CanImport())
        {
            ErrorText = "请选择受支持的本地 CSV 或 Excel 文件。";
            return;
        }

        IDataSourceReader reader = readers.First(item => item.CanRead(Path.GetExtension(FilePath)));
        char selectedDelimiter = Delimiter.Length == 1 ? Delimiter[0] : ',';
        char selectedQuote = Quote.Length == 1 ? Quote[0] : '"';
        DataImportOptions options = new()
        {
            FilePath = FilePath,
            HasHeaderRow = HasHeaderRow,
            EncodingName = EncodingName,
            Delimiter = selectedDelimiter,
            Quote = selectedQuote,
            CultureName = CultureName,
            WorksheetName = string.IsNullOrWhiteSpace(WorksheetName) ? null : WorksheetName,
            CellRange = string.IsNullOrWhiteSpace(CellRange) ? null : CellRange,
            MaximumRows = MaximumRows,
            PreviewRowLimit = PreviewRowLimit,
        };

        importCancellation?.Dispose();
        importCancellation = new CancellationTokenSource();
        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "正在读取本地数据…";
        try
        {
            DataSetSnapshot snapshot = await reader.ReadAsync(options, importCancellation.Token).ConfigureAwait(true);
            DataSet = snapshot;
            Columns.Clear();
            foreach (DataColumnDefinition column in snapshot.Schema.Columns)
            {
                Columns.Add(column);
            }

            PreviewRows.Clear();
            PreviewLines.Clear();
            foreach (DataRowSnapshot row in snapshot.Rows.Take(Math.Max(0, PreviewRowLimit)))
            {
                PreviewRows.Add(row);
                PreviewLines.Add($"{row.Index + 1}: {string.Join(" | ", snapshot.Schema.Columns.Select(column => row.GetValue(column.Name) ?? string.Empty))}");
            }

            StatusText = $"已导入 {snapshot.Count} 行，{snapshot.Schema.Columns.Count} 个字段";
            Imported?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消导入。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or DataImportException)
        {
            ErrorText = exception.Message;
            StatusText = "导入失败。";
        }
        finally
        {
            IsBusy = false;
            importCancellation?.Dispose();
            importCancellation = null;
        }
    }

    private void Cancel() => importCancellation?.Cancel();

    public void Dispose()
    {
        importCancellation?.Cancel();
        importCancellation?.Dispose();
        importCancellation = null;
    }
}
