using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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
    private int NewScope;
    private HashSet<string> NewPermissions = new();

    private IEnumerable<PermissionDTO> FilteredPermissions =>
        AvailablePermissions?.Where(p => p.Global == (NewScope == 0)) ?? [];

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
        NewScope = 0;
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
            ToastService.Error("Name ist erforderlich.");
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
                ToastService.Toast("Rolle erstellt.", "success");
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
                ToastService.Toast("Rolle gelöscht.", "success");
                await Load();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
