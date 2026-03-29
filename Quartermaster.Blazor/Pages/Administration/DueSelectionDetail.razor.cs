using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class DueSelectionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private DueSelectionDetailDTO? Selection;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Selection = await Http.GetFromJsonAsync<DueSelectionDetailDTO>(
                $"/api/admin/dueselections/{Id}");
        } catch (HttpRequestException) { }

        Loading = false;
    }

    private async Task Process(int status) {
        await Http.PostAsJsonAsync("/api/admin/dueselections/process",
            new { Id = Id, Status = status });

        ToastService.Toast(status == 1 ? "Einstufung genehmigt." : "Einstufung abgelehnt.", status == 1 ? "success" : "danger");
        NavigationManager.NavigateTo("/Administration/DueSelections");
    }

    private static string ValuationLabel(int valuation) => valuation switch {
        1 => "Monatseinkommen",
        2 => "1% Jahreseinkommen",
        3 => "Minderjährig (12€)",
        4 => "Geminderter Beitrag",
        _ => "Unbekannt"
    };

    private static string PaymentLabel(int schedule) => schedule switch {
        1 => "Jährlich",
        2 => "Quartalsweise",
        3 => "Monatlich",
        _ => "—"
    };
}
