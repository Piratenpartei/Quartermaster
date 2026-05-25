using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Permissions;
using Quartermaster.Api.Roles;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Roles;

public partial class RoleList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<RoleDTO>? Roles;
    private List<PermissionDTO>? AvailablePermissions;
    private bool Loading = true;
    private bool ShowingCreateForm;
    private bool Creating;

    private string NewName = "";
    private string NewDescription = "";
    private RoleScope NewScope;
    private HashSet<string> NewPermissions = new();

    private IEnumerable<PermissionDTO> FilteredPermissions =>
        AvailablePermissions?.Where(p => p.Global == (NewScope == RoleScope.Global)) ?? [];

    protected override async Task OnInitializedAsync() {
        await Load();
    }

    private async Task Load() {
        Loading = true;
        StateHasChanged();
        try {
            Roles = await Http.GetFromJsonAsync<List<RoleDTO>>("/api/roles");
            AvailablePermissions = await Http.GetFromJsonAsync<List<PermissionDTO>>("/api/permissions");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private void ShowCreateForm() {
        NewName = "";
        NewDescription = "";
        NewScope = RoleScope.Global;
        NewPermissions.Clear();
        ShowingCreateForm = true;
    }

    private void OnScopeChanged() {
        // Scope changed — filter permissions to match; drop ones no longer valid
        NewPermissions = new HashSet<string>(
            NewPermissions.Where(id => FilteredPermissions.Any(p => p.Identifier == id)));
    }

    private void TogglePerm(string identifier, bool checkedState) {
        if (checkedState)
            NewPermissions.Add(identifier);
        else
            NewPermissions.Remove(identifier);
    }

    private async Task CreateRole() {
        if (string.IsNullOrWhiteSpace(NewName)) {
            ToastService.ErrorKey(I18nKey.Ui.Error.NameRequired);
            return;
        }

        Creating = true;
        StateHasChanged();
        try {
            var response = await Http.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
                Name = NewName,
                Description = NewDescription,
                Scope = NewScope,
                Permissions = NewPermissions.ToList()
            });
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.RoleCreated);
                ShowingCreateForm = false;
                await Load();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Creating = false;
        StateHasChanged();
    }

    private async Task DeleteRole(RoleDTO role) {
        try {
            var response = await Http.DeleteAsync($"/api/roles/{role.Id}");
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.RoleDeleted);
                await Load();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
