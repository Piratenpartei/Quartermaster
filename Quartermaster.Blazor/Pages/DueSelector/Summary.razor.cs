using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Pages.MembershipApplication;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class Summary {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http {  get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public required string ReturnUrl { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();

        // If there's an active membership application in progress, skip this summary
        // and go directly to the membership declarations page
        var membershipState = AppState.GetEntryState<MembershipApplicationEntryState>();
        if (!string.IsNullOrEmpty(membershipState.FirstName)) {
            NavigationManager.NavigateTo("/MembershipApplication/Declarations");
            return;
        }
    }

    private async Task Submit() {
        if (EntryState == null)
            throw new UnreachableException();

        var okResponse = false;
        try {
            var result = await Http.PostAsJsonAsync("/api/dueselector", EntryState.ToDTO());
            okResponse = result.IsSuccessStatusCode;
        } catch (HttpRequestException) { }

        if (okResponse) {
            AppState.ResetEntryState<DueSelectorEntryState>();
            NavigationManager.NavigateTo("/");
            ToastService.Toast("Danke für deine Einstufung!", "success");
        } else {
            ToastService.Error();
        }
    }
}