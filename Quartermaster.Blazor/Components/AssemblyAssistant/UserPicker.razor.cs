using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class UserPicker {
    private const int MaxMatches = 12;

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyUserDTO> Users { get; set; }

    [Parameter]
    public bool CanControl { get; set; }

    [Parameter]
    public EventCallback<(uint Session, ChannelRole Target)> OnGive { get; set; }

    private string Search = "";

    private IEnumerable<AssemblyUserDTO> Matches() {
        var term = Search.Trim();
        return Users
            .Where(u => u.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .Take(MaxMatches);
    }

    private async Task Give(uint session, ChannelRole target) {
        await OnGive.InvokeAsync((session, target));
        Search = "";
    }
}
