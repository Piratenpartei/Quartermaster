using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.Accreditation;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Inputs;

/// <summary>Member number and name inputs for a Keycloak account search; the page runs the search and shows the accounts.</summary>
public partial class KeycloakAccountSearch {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<KeycloakAccountQuery> OnSearch { get; set; }

    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Prefix of the input ids, so two searches can share a page.</summary>
    [Parameter]
    public string IdPrefix { get; set; } = "keycloak-search";

    private string MemberNumber = "";
    private string Query = "";

    private bool IsEmpty => string.IsNullOrWhiteSpace(MemberNumber) && string.IsNullOrWhiteSpace(Query);

    private async Task OnKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter")
            await SearchAsync();
    }

    private async Task SearchAsync() {
        if (Busy || IsEmpty)
            return;

        await OnSearch.InvokeAsync(new KeycloakAccountQuery { MemberNumber = MemberNumber.Trim(), Query = Query.Trim() });
    }
}
