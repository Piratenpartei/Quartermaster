using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.AuditLog;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MotionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MotionDetailDTO? Motion;
    private bool Loading = true;
    private bool TogglingPublic;

    private bool EditMode;
    private bool Saving;
    private string EditTitle = "";
    private string EditTextMarkdown = "";
    private string EditAuthorName = "";
    private string EditAuthorEmail = "";

    private List<AuditLogDTO>? AuditEntries;
    private bool AuditLoading;
    private bool AuditUnavailable;

    protected override async Task OnInitializedAsync() {
        await LoadMotion();
        await LoadAuditAsync();
    }

    private async Task LoadMotion() {
        Loading = true;
        try {
            Motion = await Http.GetFromJsonAsync<MotionDetailDTO>($"/api/motions/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private bool CanEdit
        => Motion != null
            && Motion.ApprovalStatus == MotionApprovalStatus.Pending
            && AuthService.HasPermission(Motion.ChapterId, PermissionIdentifier.EditMotions)
            && Motion.TextMarkdown != null;

    private void BeginEdit() {
        if (Motion == null)
            return;
        EditTitle = Motion.Title;
        EditTextMarkdown = Motion.TextMarkdown ?? "";
        EditAuthorName = Motion.AuthorName;
        EditAuthorEmail = Motion.AuthorEmail;
        EditMode = true;
    }

    private void CancelEdit() {
        EditMode = false;
    }

    private async Task SaveEdit() {
        if (Motion == null || Saving)
            return;
        Saving = true;
        StateHasChanged();
        try {
            var resp = await Http.PutAsJsonAsync($"/api/motions/{Id}", new MotionUpdateRequest {
                Id = Id,
                Title = EditTitle,
                TextMarkdown = EditTextMarkdown,
                AuthorName = EditAuthorName,
                AuthorEmail = EditAuthorEmail,
                LinkedMembershipApplicationId = Motion.LinkedMembershipApplicationId,
                LinkedDueSelectionId = Motion.LinkedDueSelectionId
            });
            if (resp.IsSuccessStatusCode) {
                EditMode = false;
                await LoadMotion();
                await LoadAuditAsync();
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

    private async Task LoadAuditAsync() {
        AuditLoading = true;
        AuditUnavailable = false;
        try {
            var resp = await Http.GetAsync($"/api/auditlog?EntityType=Motion&EntityId={Id}");
            if (resp.IsSuccessStatusCode) {
                AuditEntries = await resp.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
            } else {
                AuditUnavailable = true;
            }
        } catch (HttpRequestException) {
            AuditUnavailable = true;
        } finally {
            AuditLoading = false;
        }
    }

    private string FieldLabel(string? fieldName) => fieldName switch {
        "Title" => I18n[I18nKey.Ui.MotionDetail.FieldTitle],
        "TextMarkdown" => I18n[I18nKey.Ui.MotionDetail.FieldBody],
        "AuthorName" => I18n[I18nKey.Ui.MotionDetail.FieldAuthorName],
        "AuthorEmail" => I18n[I18nKey.Ui.MotionDetail.FieldAuthorEmail],
        "LinkedMembershipApplicationId" => I18n[I18nKey.Ui.MotionDetail.FieldLinkedApplication],
        "LinkedDueSelectionId" => I18n[I18nKey.Ui.MotionDetail.FieldLinkedDueSelection],
        "ApprovalStatus" => I18n[I18nKey.Ui.MotionDetail.FieldStatus],
        "IsRealized" => I18n[I18nKey.Ui.MotionDetail.FieldRealized],
        "IsPublic" => I18n[I18nKey.Ui.MotionDetail.FieldVisibility],
        null => "",
        _ => fieldName
    };

    private string ActionLabel(string action) => action switch {
        "Created" => I18n[I18nKey.Ui.MotionDetail.ActionCreated],
        "Updated" => I18n[I18nKey.Ui.MotionDetail.ActionUpdated],
        "SoftDeleted" => I18n[I18nKey.Ui.MotionDetail.ActionDeleted],
        "Deleted" => I18n[I18nKey.Ui.MotionDetail.ActionDeleted],
        _ => action
    };

    private async Task CastVote(Guid memberId, VoteType vote) {
        try {
            var resp = await Http.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
                MotionId = Id,
                MemberId = memberId,
                Vote = vote
            });
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                return;
            }

            await LoadMotion();
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SetStatus(MotionApprovalStatus status) {
        try {
            await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
                MotionId = Id,
                ApprovalStatus = status
            });

            ToastService.ToastKey(I18nKey.Ui.Toast.MotionStatusUpdated);
            await LoadMotion();
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task MarkRealized() {
        try {
            await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
                MotionId = Id,
                IsRealized = true
            });

            ToastService.ToastKey(I18nKey.Ui.Toast.MotionMarkedRealized);
            await LoadMotion();
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task TogglePublic() {
        if (Motion == null) {
            return;
        }
        TogglingPublic = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
                MotionId = Id,
                IsPublic = !Motion.IsPublic
            });
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.MotionStatusUpdated);
                await LoadMotion();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            TogglingPublic = false;
            StateHasChanged();
        }
    }

    private MotionVoteDTO? GetVoteForOfficer(Guid memberId)
        => Motion?.Votes.FirstOrDefault(v => v.MemberId == memberId);

    private string OfficerRoleLabel(string role) => role switch {
        "Captain" => I18n[I18nKey.Ui.OfficerRole.Captain],
        "FirstOfficer" => I18n[I18nKey.Ui.OfficerRole.FirstOfficer],
        "Quartermaster" => I18n[I18nKey.Ui.OfficerRole.Quartermaster],
        "Treasurer" => I18n[I18nKey.Ui.OfficerRole.Treasurer],
        "ViceTreasurer" => I18n[I18nKey.Ui.OfficerRole.ViceTreasurer],
        "PoliticalDirector" => I18n[I18nKey.Ui.OfficerRole.PoliticalDirector],
        "Member" => I18n[I18nKey.Ui.OfficerRole.Member],
        _ => role
    };
}
