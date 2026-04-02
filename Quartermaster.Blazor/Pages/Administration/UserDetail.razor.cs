using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Permissions;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public class UserDetailResponse {
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string EMail { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public partial class UserDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private UserDetailResponse? UserInfo;
    private UserPermissionsDTO? UserPermissions;
    private List<PermissionDTO>? AllPermissions;
    private List<ChapterDTO>? Chapters;
    private bool Loading = true;

    private string NewChapterId = "";
    private string NewPermissionIdentifier = "";

    private List<PermissionDTO>? GlobalPermissions
        => AllPermissions?.Where(p => p.Global).ToList();

    private List<PermissionDTO>? ChapterPermissions
        => AllPermissions?.Where(p => !p.Global).ToList();

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        UserInfo = null;
        UserPermissions = null;

        try {
            UserInfo = await Http.GetFromJsonAsync<UserDetailResponse>($"/api/users/{Id}");
            UserPermissions = await Http.GetFromJsonAsync<UserPermissionsDTO>($"/api/users/{Id}/permissions");
            AllPermissions ??= await Http.GetFromJsonAsync<List<PermissionDTO>>("/api/permissions");
            Chapters ??= await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private bool IsGlobalPermissionGranted(string identifier)
        => UserPermissions?.GlobalPermissions.Contains(identifier) ?? false;

    private async Task ToggleGlobalPermission(string identifier, bool grant) {
        try {
            if (grant) {
                var response = await Http.PostAsJsonAsync(
                    $"/api/users/{Id}/permissions/global",
                    new { permissionIdentifier = identifier });
                response.EnsureSuccessStatusCode();
                if (UserPermissions != null && !UserPermissions.GlobalPermissions.Contains(identifier))
                    UserPermissions.GlobalPermissions.Add(identifier);
            } else {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{Id}/permissions/global") {
                    Content = JsonContent.Create(new { permissionIdentifier = identifier })
                };
                var response = await Http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                UserPermissions?.GlobalPermissions.Remove(identifier);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            // Reload to get correct state
            UserPermissions = await Http.GetFromJsonAsync<UserPermissionsDTO>($"/api/users/{Id}/permissions");
        }
    }

    private async Task AddChapterPermission() {
        if (string.IsNullOrEmpty(NewChapterId) || string.IsNullOrEmpty(NewPermissionIdentifier))
            return;

        try {
            var response = await Http.PostAsJsonAsync(
                $"/api/users/{Id}/permissions/chapter",
                new { chapterId = NewChapterId, permissionIdentifier = NewPermissionIdentifier });
            response.EnsureSuccessStatusCode();

            // Reload permissions
            UserPermissions = await Http.GetFromJsonAsync<UserPermissionsDTO>($"/api/users/{Id}/permissions");
            NewPermissionIdentifier = "";
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task RevokeChapterPermission(string chapterId, string permissionIdentifier) {
        try {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{Id}/permissions/chapter") {
                Content = JsonContent.Create(new { chapterId, permissionIdentifier })
            };
            var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Reload permissions
            UserPermissions = await Http.GetFromJsonAsync<UserPermissionsDTO>($"/api/users/{Id}/permissions");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private string GetChapterName(string chapterId) {
        if (Chapters == null || !Guid.TryParse(chapterId, out var id))
            return chapterId;
        var chapter = Chapters.FirstOrDefault(c => c.Id == id);
        return chapter?.Name ?? chapterId;
    }

    private string GetPermissionDisplayName(string identifier) {
        var perm = AllPermissions?.FirstOrDefault(p => p.Identifier == identifier);
        return perm?.DisplayName ?? identifier;
    }
}
