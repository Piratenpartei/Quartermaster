using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.Attributions;

public partial class Attributions {
    [Inject]
    public required I18nService I18n { get; set; }

    private static readonly LibraryScope[] Scopes = [LibraryScope.Server, LibraryScope.Browser];

    private static IEnumerable<OpenSourceLibrary> LibrariesIn(LibraryScope scope) {
        return OpenSourceLibraries.All
            .Where(l => l.Scope == scope)
            .OrderBy(l => l.Name);
    }

    private string ScopeLabel(LibraryScope scope) {
        return scope == LibraryScope.Server ? I18n[I18nKey.Ui.Attributions.ScopeServer] : I18n[I18nKey.Ui.Attributions.ScopeBrowser];
    }
}
