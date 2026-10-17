using System.Collections.Generic;

namespace Quartermaster.Blazor.Pages.Attributions;

public enum LibraryScope {
    Server,
    Browser
}

/// <param name="Packages">The NuGet package ids this entry covers; empty for vendored browser files and protocol definitions.</param>
public record OpenSourceLibrary(string Name, string License, string Url, LibraryScope Scope, IReadOnlyList<string> Packages);
