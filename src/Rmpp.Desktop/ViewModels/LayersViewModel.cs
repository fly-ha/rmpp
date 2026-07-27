using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;

namespace Rmpp.Desktop.ViewModels;

public sealed class LayersViewModel : ObservableObject, IDisposable
{
    private readonly DocumentSession session;
    private readonly EditorCommandDispatcher dispatcher;

    public LayersViewModel(DocumentSession session, EditorCommandDispatcher dispatcher)
    {
        this.session = session;
        this.dispatcher = dispatcher;
        ToggleVisibleCommand = new RelayCommand<LayerDefinition>(ToggleVisible);
        ToggleLockedCommand = new RelayCommand<LayerDefinition>(ToggleLocked);
        TogglePrintableCommand = new RelayCommand<LayerDefinition>(TogglePrintable);
        Refresh();
        session.Changed += OnSessionChanged;
    }

    public ObservableCollection<LayerDefinition> Layers { get; } = [];
    public IRelayCommand<LayerDefinition> ToggleVisibleCommand { get; }
    public IRelayCommand<LayerDefinition> ToggleLockedCommand { get; }
    public IRelayCommand<LayerDefinition> TogglePrintableCommand { get; }

    public void Dispose() => session.Changed -= OnSessionChanged;

    private void ToggleVisible(LayerDefinition? layer) => Change(layer, current => current with { IsVisible = !current.IsVisible });
    private void ToggleLocked(LayerDefinition? layer) => Change(layer, current => current with { IsLocked = !current.IsLocked });
    private void TogglePrintable(LayerDefinition? layer) => Change(layer, current => current with { IsPrintable = !current.IsPrintable });

    private void Change(LayerDefinition? layer, Func<LayerDefinition, LayerDefinition> transform)
    {
        if (layer is not null)
        {
            _ = dispatcher.Execute(new ChangeLayerPropertiesCommand(layer.Id, transform));
        }
    }

    private void OnSessionChanged(object? sender, DocumentSessionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Layers.Clear();
        foreach (LayerDefinition layer in session.State.Document.Layers)
        {
            Layers.Add(layer);
        }
    }
}
