using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
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
    public string? AdministrativeDivisionName { get; set; }
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
    private string EditExternalCode = "";
    private string EditParentChapterId = "";
    private string EditAdministrativeDivisionId = "";

    private ConfirmDialog DeleteConfirm = default!;

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
        EditExternalCode = Detail.Chapter.ExternalCode ?? "";
        EditParentChapterId = Detail.Chapter.ParentChapterId?.ToString() ?? "";
        EditAdministrativeDivisionId = Detail.Chapter.AdministrativeDivisionId?.ToString() ?? "";
        Editing = true;
    }

    private void CancelEdit() {
        Editing = false;
    }

    private void OnParentChanged(string id) {
        EditParentChapterId = id;
    }

    private void OnDivisionChanged(string id) {
        EditAdministrativeDivisionId = id;
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
                ExternalCode = string.IsNullOrWhiteSpace(EditExternalCode) ? null : EditExternalCode.Trim(),
                ParentChapterId = Guid.TryParse(EditParentChapterId, out var parsed) ? parsed : null,
                AdministrativeDivisionId = Guid.TryParse(EditAdministrativeDivisionId, out var divParsed) ? divParsed : null
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
