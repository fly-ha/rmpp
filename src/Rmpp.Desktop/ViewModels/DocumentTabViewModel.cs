using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Data;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Desktop.Resources;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Desktop.ViewModels;

/// <summary>每个标签页拥有独立会话、撤销历史、选择、视口和属性/图层模型。</summary>
public sealed class DocumentTabViewModel : ObservableObject, IDisposable
{
    public DocumentTabViewModel(
        TemplateDocument document,
        IReadOnlyDictionary<Guid, byte[]>? assets = null,
        string? originalTemplatePath = null)
    {
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> sessionAssets = assets is null
            ? new Dictionary<Guid, ReadOnlyMemory<byte>>()
            : assets.ToDictionary(static pair => pair.Key, static pair => (ReadOnlyMemory<byte>)pair.Value);
        Session = new DocumentSession(document, originalTemplatePath, sessionAssets);
        Dispatcher = new EditorCommandDispatcher(Session);
        Designer = new DesignerViewModel(Session, Dispatcher);
        Properties = new PropertiesViewModel(Session, Dispatcher);
        Layers = new LayersViewModel(Session, Dispatcher);
        Status = new StatusBarViewModel();
        DataPreview = new DataPreviewViewModel(Session);
        ExpressionEditor = new ExpressionEditorViewModel();
        UndoCommand = new RelayCommand(Undo, () => Dispatcher.History.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => Dispatcher.History.CanRedo);
        DeleteCommand = new RelayCommand(DeleteSelection, () => Session.State.SelectedElementIds.Count > 0);
        DuplicateCommand = new RelayCommand(Designer.DuplicateSelection, () => Session.State.SelectedElementIds.Count > 0);
        Session.Changed += OnSessionChanged;
    }

    public DocumentSession Session { get; }
    public EditorCommandDispatcher Dispatcher { get; }
    public DesignerViewModel Designer { get; }
    public PropertiesViewModel Properties { get; }
    public LayersViewModel Layers { get; }
    public StatusBarViewModel Status { get; }
    public DataPreviewViewModel DataPreview { get; }
    public ExpressionEditorViewModel ExpressionEditor { get; }
    public DataSetSnapshot? DataSet { get; private set; }
    public IReadOnlyDictionary<Guid, byte[]> Assets => Session.State.Document.Assets
        .Where(asset => Session.State.AssetContents.ContainsKey(asset.Id))
        .ToDictionary(
            static asset => asset.Id,
            asset => Session.State.AssetContents[asset.Id].ToArray());
    public string? OriginalTemplatePath => Session.State.FilePath;
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand DuplicateCommand { get; }
    public string DisplayTitle => Session.State.Document.Metadata.Title;
    public bool IsDirty => Session.State.IsDirty;

    public TemplatePackageContent CreateRecoveryContent()
    {
        TemplateDocument document = Session.State.Document;
        Dictionary<Guid, byte[]> assets = document.Assets.ToDictionary(
            static asset => asset.Id,
            asset => Session.State.AssetContents.TryGetValue(asset.Id, out ReadOnlyMemory<byte> content)
                ? content.ToArray()
                : throw new InvalidOperationException($"Missing asset content: {asset.FileName}"));
        return new TemplatePackageContent { Document = document, Assets = assets };
    }

    /// <summary>把导入数据附加到当前标签页的内存会话，不写入文档或恢复状态。</summary>
    public void AttachDataSet(DataSetSnapshot dataSet)
    {
        DataSet = dataSet ?? throw new ArgumentNullException(nameof(dataSet));
        DataPreview.Load(dataSet, Session.State.Document);
        ExpressionEditor.SetFields(dataSet.Schema.Columns.Select(static column => column.Name));
        if (dataSet.Rows.Count > 0) ExpressionEditor.SetSampleValues(dataSet.Rows[0].Values);
        OnPropertyChanged(nameof(DataSet));
    }

    public void Dispose()
    {
        Session.Changed -= OnSessionChanged;
        Designer.Dispose();
        Properties.Dispose();
        Layers.Dispose();
        DataPreview.Dispose();
    }

    private void Undo()
    {
        _ = Dispatcher.Undo();
    }

    private void Redo()
    {
        _ = Dispatcher.Redo();
    }

    private void DeleteSelection()
    {
        _ = Dispatcher.Execute(new DeleteElementsCommand(Session.State.SelectedElementIds));
    }

    private void OnSessionChanged(object? sender, DocumentSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(Assets));
        OnPropertyChanged(nameof(OriginalTemplatePath));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        Status.Text = e.State.ValidationIssues.Count == 0
            ? DesktopText.Format("ElementCountFormat", e.State.Document.Elements.Count)
            : DesktopText.Format("IssueCountFormat", e.State.ValidationIssues.Count);
    }
}
