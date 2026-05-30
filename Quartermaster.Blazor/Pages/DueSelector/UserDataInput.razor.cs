using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class UserDataInput {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override async Task OnInitializedAsync() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
        await AuthService.WaitForInitializationAsync();
        var user = AuthService.CurrentUser;
        if (user != null) {
            EntryState.FirstName = user.FirstName;
            EntryState.LastName = user.LastName;
            EntryState.Email = user.Email;
        }
    }

    private bool CanContinue() {
        if (EntryState == null)
            return false;

        if (string.IsNullOrEmpty(EntryState.FirstName))
            return false;
        if (string.IsNullOrEmpty(EntryState.LastName))
            return false;
        if (string.IsNullOrEmpty(EntryState.Email))
            return false;
        if (EntryState.MemberNumber == 0)
            return false;

        return true;
    }
}