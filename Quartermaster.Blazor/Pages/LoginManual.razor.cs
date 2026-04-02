using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class LoginManual {
    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    private string UsernameOrEmail = "";
    private string Password = "";
    private bool Submitting;

    private bool IsFormValid =>
        !string.IsNullOrWhiteSpace(UsernameOrEmail) && !string.IsNullOrWhiteSpace(Password);

    private async Task HandleLogin() {
        if (!IsFormValid)
            return;

        Submitting = true;
        StateHasChanged();

        try {
            var success = await AuthService.LoginAsync(UsernameOrEmail, Password);
            if (success) {
                var returnUrl = await AuthService.GetReturnUrlAsync();
                await AuthService.ClearReturnUrlAsync();
                Navigation.NavigateTo(returnUrl ?? "/", forceLoad: false);
            } else {
                ToastService.Toast("Anmeldung fehlgeschlagen. Bitte Zugangsdaten prüfen.", "danger");
            }
        } catch {
            ToastService.Toast("Fehler bei der Anmeldung.", "danger");
        }

        Submitting = false;
        StateHasChanged();
    }
}
