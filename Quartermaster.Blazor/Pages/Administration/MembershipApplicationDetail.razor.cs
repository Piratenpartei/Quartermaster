using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MembershipApplicationDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MembershipApplicationDetailDTO? App;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            App = await Http.GetFromJsonAsync<MembershipApplicationDetailDTO>(
                $"/api/admin/membershipapplications/{Id}");
        } catch (HttpRequestException) { }

        Loading = false;
    }

    private static string ValuationLabel(int valuation) => valuation switch {
        1 => "Monatseinkommen",
        2 => "1% Jahreseinkommen",
        3 => "Minderjährig (12€)",
        4 => "Geminderter Beitrag",
        _ => "Unbekannt"
    };

    private static string DueStatusLabel(int status) => status switch {
        0 => "Ausstehend",
        1 => "Genehmigt",
        2 => "Abgelehnt",
        3 => "Automatisch genehmigt",
        _ => "Unbekannt"
    };
}
