using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Forwarding;

/// <summary>Edits one target project in place. Without lookups (no connection or Redmine unreachable) the ids are typed in.</summary>
public partial class ForwardingTargetEditor {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required ForwardingTargetDTO Target { get; set; }

    [Parameter]
    public RedmineLookupDTO? Lookups { get; set; }

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public bool ShowNoteFields { get; set; }

    [Parameter]
    public string IdPrefix { get; set; } = "target";

    private void AddField() {
        Target.CustomFields.Add(new ForwardingCustomFieldDTO());
    }
}
