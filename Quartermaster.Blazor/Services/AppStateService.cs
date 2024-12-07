using Quartermaster.Blazor.Components;

namespace Quartermaster.Blazor.Services;

public class AppStateService {
    public Theme SelectedTheme { get; set; } = Theme.Dark;
}