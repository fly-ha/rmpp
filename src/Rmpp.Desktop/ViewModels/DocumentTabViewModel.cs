using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Documents;
using Rmpp.Application.Data;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;
using Rmpp.Desktop.Resources;
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
        Session = new DocumentSession(document);
        Dispatcher = new EditorCommandDispatcher(Session);
        Designer = new DesignerViewModel(Session, Dispatcher);
        Properties = new PropertiesViewModel(Session, Dispatcher);
        Layers = new LayersViewModel(Session, Dispatcher);
        Status = new StatusBarViewModel();
        DataPreview = new DataPreviewViewModel();
        ExpressionEditor = new ExpressionEditorViewModel();
        UndoCommand = new RelayCommand(Undo, () => Dispatcher.History.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => Dispatcher.History.CanRedo);
        DeleteCommand = new RelayCommand(DeleteSelection, () => Session.State.SelectedElementIds.Count > 0);
        DuplicateCommand = new RelayCommand(Designer.DuplicateSelection, () => Session.State.SelectedElementIds.Count > 0);
        Session.Changed += OnSessionChanged;
        Assets = assets ?? new Dictionary<Guid, byte[]>();
        OriginalTemplatePath = originalTemplatePath;
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
    public IReadOnlyDictionary<Guid, byte[]> Assets { get; }
    public string? OriginalTemplatePath { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand DuplicateCommand { get; }
    public string DisplayTitle => Session.State.Document.Metadata.Title;
    public bool IsDirty => Session.State.IsDirty;

    public TemplatePackageContent CreateRecoveryContent() => new()
    {
        Document = Session.State.Document,
        Assets = Assets,
    };

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
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        Status.Text = e.State.ValidationIssues.Count == 0
            ? DesktopText.Format("ElementCountFormat", e.State.Document.Elements.Count)
            : DesktopText.Format("IssueCountFormat", e.State.ValidationIssues.Count);
    }
}
