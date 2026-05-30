using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class PersonalData {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private MembershipApplicationEntryState? EntryState;

    protected override async Task OnInitializedAsync() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
        await AuthService.WaitForInitializationAsync();
    }

    private bool CanContinue() {
        if (EntryState == null)
            return false;
        if (string.IsNullOrEmpty(EntryState.FirstName))
            return false;
        if (string.IsNullOrEmpty(EntryState.LastName))
            return false;
        if (EntryState.DateOfBirth == null)
            return false;
        if (string.IsNullOrEmpty(EntryState.Citizenship))
            return false;
        if (string.IsNullOrEmpty(EntryState.Email))
            return false;
        return true;
    }
}
