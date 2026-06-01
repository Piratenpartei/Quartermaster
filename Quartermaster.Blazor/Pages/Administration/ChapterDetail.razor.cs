using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public class ChapterDetailResponse {
    public ChapterDTO Chapter { get; set; } = new();
    public Guid? ParentChapterId { get; set; }
    public string? ParentChapterName { get; set; }
    public List<string> AdministrativeDivisionNames { get; set; } = [];
    public List<ChapterOfficerDTO> Officers { get; set; } = new();
    public List<ChapterDTO> Children { get; set; } = new();
}

public partial class ChapterDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ChapterDetailResponse? Detail;
    private bool Loading = true;
    private bool Editing;
    private bool Saving;

    private string EditName = "";
    private string EditShortCode = "";
    private List<string> EditExternalCodes = [];
    private List<Guid> EditAdministrativeDivisionIds = [];
    private List<string> EditAdministrativeDivisionNames = [];
    private string EditParentChapterId = "";

    private ConfirmDialog DeleteConfirm = default!;
    private ConfirmDialog ArchiveConfirm = default!;
    private ConfirmDialog UnarchiveConfirm = default!;
    private ConfirmDialog RemoveOfficerConfirm = default!;

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        Detail = null;
        Editing = false;

        try {
            Detail = await Http.GetFromJsonAsync<ChapterDetailResponse>($"/api/chapters/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private void BeginEdit() {
        if (Detail == null) {
            return;
        }
        EditName = Detail.Chapter.Name;
        EditShortCode = Detail.Chapter.ShortCode ?? "";
        EditExternalCodes = [.. Detail.Chapter.ExternalCodes];
        EditAdministrativeDivisionIds = [.. Detail.Chapter.AdministrativeDivisionIds];
        EditAdministrativeDivisionNames = [.. Detail.AdministrativeDivisionNames];
        EditParentChapterId = Detail.Chapter.ParentChapterId?.ToString() ?? "";
        Editing = true;
    }

    private void CancelEdit() {
        Editing = false;
    }

    private void OnParentChanged(string id) {
        EditParentChapterId = id;
    }

    private void OnExternalCodesChanged(List<string> codes) {
        EditExternalCodes = codes;
    }

    private void OnDivisionsChanged(List<Guid> ids) {
        EditAdministrativeDivisionIds = ids;
    }

    private async Task SaveEdit() {
        if (Detail == null) {
            return;
        }
        Saving = true;
        StateHasChanged();
        try {
            var req = new ChapterUpdateRequest {
                Name = EditName.Trim(),
                ShortCode = string.IsNullOrWhiteSpace(EditShortCode) ? null : EditShortCode.Trim(),
                ExternalCodes = EditExternalCodes,
                AdministrativeDivisionIds = EditAdministrativeDivisionIds,
                ParentChapterId = Guid.TryParse(EditParentChapterId, out var parsed) ? parsed : null
            };
            var resp = await Http.PutAsJsonAsync($"/api/chapters/{Id}", req);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                Editing = false;
                await ReloadDetail();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Saving = false;
            StateHasChanged();
        }
    }

    private async Task ConfirmDelete() {
        if (Detail == null) {
            return;
        }
        var ok = await DeleteConfirm.ShowAsync(I18n[$"{I18nKey.Ui.ChapterDetail.DeleteDialogMessage}?name={Detail.Chapter.Name}"]);
        if (!ok) {
            return;
        }
        try {
            var resp = await Http.DeleteAsync($"/api/chapters/{Id}");
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                Navigation.NavigateTo("/Administration/Chapters");
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ConfirmArchive() {
        if (Detail == null) {
            return;
        }
        var ok = await ArchiveConfirm.ShowAsync(I18n[$"{I18nKey.Ui.ChapterDetail.ArchiveDialogMessage}?name={Detail.Chapter.Name}"]);
        if (!ok) {
            return;
        }
        await ToggleArchive(archive: true);
    }

    private async Task ConfirmUnarchive() {
        if (Detail == null) {
            return;
        }
        var ok = await UnarchiveConfirm.ShowAsync(I18n[$"{I18nKey.Ui.ChapterDetail.UnarchiveDialogMessage}?name={Detail.Chapter.Name}"]);
        if (!ok) {
            return;
        }
        await ToggleArchive(archive: false);
    }

    private async Task ToggleArchive(bool archive) {
        try {
            var resp = await Http.PutAsync($"/api/chapters/{Id}/{(archive ? "archive" : "unarchive")}", null);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                await ReloadDetail();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ConfirmRemoveOfficer(ChapterOfficerDTO officer) {
        if (Detail == null) {
            return;
        }
        var name = $"{officer.MemberFirstName} {officer.MemberLastName}".Trim();
        var ok = await RemoveOfficerConfirm.ShowAsync(
            I18n[$"{I18nKey.Ui.ChapterDetail.RemoveOfficerDialogMessage}?name={name}&chapter={Detail.Chapter.Name}"]);
        if (!ok) {
            return;
        }
        try {
            var req = new HttpRequestMessage(HttpMethod.Delete, "/api/chapterofficers") {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { MemberId = officer.MemberId, ChapterId = officer.ChapterId }),
                    Encoding.UTF8) {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };
            var resp = await Http.SendAsync(req);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                await ReloadDetail();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReloadDetail() {
        try {
            Detail = await Http.GetFromJsonAsync<ChapterDetailResponse>($"/api/chapters/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private string RoleLabel(ChapterOfficerType role) => role switch {
        ChapterOfficerType.Captain => I18n[I18nKey.Ui.OfficerRole.Captain],
        ChapterOfficerType.FirstOfficer => I18n[I18nKey.Ui.OfficerRole.FirstOfficer],
        ChapterOfficerType.Quartermaster => I18n[I18nKey.Ui.OfficerRole.Quartermaster],
        ChapterOfficerType.Treasurer => I18n[I18nKey.Ui.OfficerRole.Treasurer],
        ChapterOfficerType.ViceTreasurer => I18n[I18nKey.Ui.OfficerRole.ViceTreasurer],
        ChapterOfficerType.PoliticalDirector => I18n[I18nKey.Ui.OfficerRole.PoliticalDirector],
        ChapterOfficerType.Member => I18n[I18nKey.Ui.OfficerRole.Member],
        _ => role.ToString()
    };
}
