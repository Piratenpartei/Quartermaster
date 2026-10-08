using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Quartermaster.Api.Accreditation;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class Accreditation {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required IJSRuntime JS { get; set; }

    private string usernameSeparator = ".";
    private string memberNumberQuery = "";
    private bool searchBusy;
    private bool setEligibleBusy;
    private bool notConfigured;
    private List<AccreditationAccountDTO>? searchResults;
    private ConfirmDialog? setEligibleConfirm;

    private string newFirstName = "";
    private string newLastName = "";
    private string newMemberNumber = "";
    private string newUsername = "";
    private string newEmail = "";
    private bool newEligible = true;
    private bool usernameEdited;
    private bool createBusy;
    private bool checkBusy;
    private TemporaryAccountCheckDTO? checkResult;
    private bool copied;
    private TemporaryAccountCreatedDTO? created;
    private List<AccreditationAccountDTO>? conflictAccounts;

    private bool CanCreate => checkResult != null && checkResult.Ok && !createBusy && !checkBusy;

    protected override async Task OnInitializedAsync() {
        try {
            var config = await Http.GetFromJsonAsync<AccreditationConfigDTO>("/api/accreditation/config");
            if (config != null) {
                usernameSeparator = config.UsernameSeparator;
                notConfigured = !config.KeycloakConfigured;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task OnMemberNumberKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter")
            await SearchAsync();
    }

    private async Task SearchAsync() {
        if (string.IsNullOrWhiteSpace(memberNumberQuery))
            return;

        searchBusy = true;
        notConfigured = false;
        searchResults = null;
        try {
            var response = await Http.GetAsync($"/api/accreditation/members/{Uri.EscapeDataString(memberNumberQuery.Trim())}");
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable) {
                notConfigured = true;
                return;
            }
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            searchResults = await response.Content.ReadFromJsonAsync<List<AccreditationAccountDTO>>() ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            searchBusy = false;
        }
    }

    private async Task SetEligibleAsync(AccreditationAccountDTO account) {
        if (setEligibleConfirm == null)
            return;

        var fullName = $"{account.FirstName} {account.LastName}";
        var confirmed = await setEligibleConfirm.ShowAsync(
            I18n[$"{I18nKey.Ui.Accreditation.SetEligibleConfirm}?name={fullName}&number={account.MemberNumber}"]);
        if (!confirmed)
            return;

        setEligibleBusy = true;
        try {
            var response = await Http.PostAsync($"/api/accreditation/members/{Uri.EscapeDataString(account.MemberNumber)}/eligible", null);
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            var updated = await response.Content.ReadFromJsonAsync<AccreditationAccountDTO>();
            if (updated != null)
                searchResults = [updated];
            ToastService.ToastKey(I18nKey.Ui.Accreditation.SetEligibleDone);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            setEligibleBusy = false;
        }
    }

    private void OnUsernameEdited() {
        usernameEdited = true;
        InvalidateCheck();
    }

    private void OnNameChanged() {
        if (!usernameEdited)
            newUsername = UsernameRule.BuildBaseName(newFirstName, newLastName, usernameSeparator);
        InvalidateCheck();
    }

    private void InvalidateCheck() {
        checkResult = null;
        conflictAccounts = null;
    }

    private void AcceptSuggestion() {
        if (string.IsNullOrEmpty(checkResult?.SuggestedUsername))
            return;
        newUsername = checkResult.SuggestedUsername;
        usernameEdited = true;
        InvalidateCheck();
    }

    private async Task CheckAsync() {
        checkBusy = true;
        InvalidateCheck();
        try {
            var response = await Http.PostAsJsonAsync("/api/accreditation/temporary-accounts/check", BuildRequest());
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            checkResult = await response.Content.ReadFromJsonAsync<TemporaryAccountCheckDTO>();
            if (checkResult != null && checkResult.ExistingAccounts.Count > 0)
                conflictAccounts = checkResult.ExistingAccounts;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            checkBusy = false;
        }
    }

    private TemporaryAccountRequest BuildRequest() {
        return new TemporaryAccountRequest {
            FirstName = newFirstName.Trim(),
            LastName = newLastName.Trim(),
            MemberNumber = newMemberNumber.Trim(),
            Username = string.IsNullOrWhiteSpace(newUsername) ? null : newUsername.Trim(),
            Email = string.IsNullOrWhiteSpace(newEmail) ? null : newEmail.Trim(),
            Eligible = newEligible
        };
    }

    private IEnumerable<string> ProblemsFor(string field) {
        if (checkResult == null)
            return [];
        return checkResult.Problems
            .Where(p => p.Field == field)
            .Select(p => p.Message);
    }

    private string InvalidClass(string field) => ProblemsFor(field).Any() ? "is-invalid" : "";

    private async Task CreateAsync() {
        if (!CanCreate)
            return;

        createBusy = true;
        checkResult = null;
        created = null;
        conflictAccounts = null;
        copied = false;
        try {
            var response = await Http.PostAsJsonAsync("/api/accreditation/temporary-accounts", BuildRequest());
            if (response.StatusCode == HttpStatusCode.Conflict) {
                var conflict = await response.Content.ReadFromJsonAsync<TemporaryAccountConflictDTO>();
                conflictAccounts = conflict?.ExistingAccounts ?? [];
                return;
            }
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            created = await response.Content.ReadFromJsonAsync<TemporaryAccountCreatedDTO>();
            ResetTemporaryAccountForm();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            createBusy = false;
        }
    }

    private void ResetTemporaryAccountForm() {
        newFirstName = "";
        newLastName = "";
        newMemberNumber = "";
        newUsername = "";
        newEmail = "";
        newEligible = true;
        usernameEdited = false;
        checkResult = null;
    }

    private void DismissCreated() {
        created = null;
        copied = false;
    }

    private async Task CopyCredentialsAsync() {
        if (created == null)
            return;

        var text = $"Benutzername: {created.Username}\nPasswort: {created.Password}";
        try {
            copied = await JS.InvokeAsync<bool>("CopyToClipboard", text);
        } catch (JSException ex) {
            ToastService.Error(ex);
        }
    }

    private string AccountStatusLabel(AccreditationAccountDTO account) {
        if (account.Closed)
            return I18n[I18nKey.Ui.Accreditation.StatusClosed];
        if (!account.Enabled)
            return I18n[I18nKey.Ui.Accreditation.StatusDisabled];
        return I18n[I18nKey.Ui.Accreditation.StatusActive];
    }

    private string YesNo(bool value) => value ? I18n[I18nKey.Ui.Accreditation.Yes] : I18n[I18nKey.Ui.Accreditation.No];
}
