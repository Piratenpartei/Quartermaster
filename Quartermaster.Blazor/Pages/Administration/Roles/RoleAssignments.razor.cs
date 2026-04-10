using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Roles;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Roles;

public partial class RoleAssignments {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<UserRoleAssignmentDTO>? Assignments;
    private List<RoleDTO>? Roles;
    private List<UserListItem>? Users;
    private bool Loading = true;
    private bool ShowingCreateForm;
    private bool Creating;

    private string NewUserId = "";
    private string NewRoleId = "";
    private string NewChapterIdString = "";
    private int SelectedRoleScope = -1;

    protected override async Task OnInitializedAsync() {
        await Load();
    }

    private async Task Load() {
        Loading = true;
        StateHasChanged();
        try {
            Assignments = await Http.GetFromJsonAsync<List<UserRoleAssignmentDTO>>("/api/roleassignments");
            Roles = await Http.GetFromJsonAsync<List<RoleDTO>>("/api/roles");
            Users = await Http.GetFromJsonAsync<List<UserListItem>>("/api/users");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private void OnRoleChanged() {
        if (Guid.TryParse(NewRoleId, out var roleId) && Roles != null) {
            var role = Roles.FirstOrDefault(r => r.Id == roleId);
            SelectedRoleScope = role?.Scope ?? -1;
            if (SelectedRoleScope == 0) {
                NewChapterIdString = "";
            }
        } else {
            SelectedRoleScope = -1;
        }
    }

    private async Task CreateAssignment() {
        if (!Guid.TryParse(NewUserId, out var userId)) {
            ToastService.Error("Bitte Benutzer auswählen.");
            return;
        }
        if (!Guid.TryParse(NewRoleId, out var roleId)) {
            ToastService.Error("Bitte Rolle auswählen.");
            return;
        }

        Guid? chapterId = null;
        if (SelectedRoleScope == 1) {
            if (!Guid.TryParse(NewChapterIdString, out var parsedChapter)) {
                ToastService.Error("Bitte Gliederung auswählen.");
                return;
            }
            chapterId = parsedChapter;
        }

        Creating = true;
        StateHasChanged();
        try {
            var response = await Http.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
                UserId = userId,
                RoleId = roleId,
                ChapterId = chapterId
            });
            if (response.IsSuccessStatusCode) {
                ToastService.Toast("Zuweisung erstellt.", "success");
                NewUserId = "";
                NewRoleId = "";
                NewChapterIdString = "";
                SelectedRoleScope = -1;
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

    private async Task RemoveAssignment(UserRoleAssignmentDTO assignment) {
        try {
            var response = await Http.DeleteAsync($"/api/roleassignments/{assignment.Id}");
            if (response.IsSuccessStatusCode) {
                ToastService.Toast("Zuweisung entfernt.", "success");
                await Load();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}

