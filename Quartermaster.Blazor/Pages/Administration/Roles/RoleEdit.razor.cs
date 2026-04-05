using System;
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

public partial class RoleEdit {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private RoleDTO? Role;
    private List<PermissionDTO>? AvailablePermissions;
    private bool Loading = true;
    private bool Saving;

    private IEnumerable<PermissionDTO> FilteredPermissions =>
        AvailablePermissions?.Where(p => Role != null && p.Global == (Role.Scope == 0)) ?? [];

    protected override async Task OnInitializedAsync() {
        try {
            var roles = await Http.GetFromJsonAsync<List<RoleDTO>>("/api/roles");
            Role = roles?.FirstOrDefault(r => r.Id == Id);
            AvailablePermissions = await Http.GetFromJsonAsync<List<PermissionDTO>>("/api/permissions");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private void TogglePerm(string identifier, bool checkedState) {
        if (Role == null)
            return;
        if (checkedState) {
            if (!Role.Permissions.Contains(identifier))
                Role.Permissions.Add(identifier);
        } else {
            Role.Permissions.Remove(identifier);
        }
    }

    private async Task SaveRole() {
        if (Role == null)
            return;
        Saving = true;
        StateHasChanged();
        try {
            var response = await Http.PutAsJsonAsync($"/api/roles/{Role.Id}", new RoleUpdateRequest {
                Id = Role.Id,
                Name = Role.Name,
                Description = Role.Description,
                Permissions = Role.Permissions
            });
            if (response.IsSuccessStatusCode) {
                ToastService.Toast("Gespeichert.", "success");
            } else {
                var body = await response.Content.ReadAsStringAsync();
                ToastService.Error(details: body);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Saving = false;
        StateHasChanged();
    }
}
