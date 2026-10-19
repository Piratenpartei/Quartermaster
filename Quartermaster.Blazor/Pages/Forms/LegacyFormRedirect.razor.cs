using System;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Pages.Forms;

/// <summary>Keeps links to the former German form addresses working by forwarding them to the English routes.</summary>
public partial class LegacyFormRedirect {
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Parameter]
    public Guid? Id { get; set; }

    protected override void OnInitialized() {
        var uri = new Uri(Navigation.Uri);
        var path = uri.AbsolutePath.TrimEnd('/');
        var target = path switch {
            "/Formulare/meine" => "/Forms/Mine",
            "/Formulare" => "/Forms",
            _ => Id == null ? "/Forms/ExpenseReimbursement" : $"/Forms/ExpenseReimbursement/{Id}"
        };
        Navigation.NavigateTo(target + uri.Query, replace: true);
    }
}
