using Quartermaster.Blazor.Abstract;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Pages.DueSelector;

namespace Quartermaster.Blazor.Services;

public class AppStateService {
    public Theme SelectedTheme { get; set; } = Theme.Dark;

    private readonly Dictionary<Type, EntryStateBase> _entryStates = [];

    public AppStateService() {
        SupplementEntryState<DueSelectorEntryState>();
    }

    private void SupplementEntryState<T>() where T : EntryStateBase, new() {
        if (_entryStates.ContainsKey(typeof(T)))
            return;

        _entryStates.Add(typeof(T), new T());
    }

    public T GetEntryState<T>() where T : EntryStateBase {
        if (_entryStates.TryGetValue(typeof(T), out var entryStateBase) && entryStateBase is T t)
            return t;

        throw new ArgumentException($"{typeof(T)} does not have a registered EntryState!");
    }
}