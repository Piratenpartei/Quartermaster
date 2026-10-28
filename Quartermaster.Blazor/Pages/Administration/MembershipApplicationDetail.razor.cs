using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MembershipApplicationDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ApplicantRegisterService Register { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MembershipApplicationDetailDTO? App;
    private bool Loading = true;
    private int? WelcomeMemberNumber;
    private bool Sending;
    private AdministrativeDivisionDTO? SelectedDivision;
    private string? SelectedChapterName;
    private bool Linking;
    private bool Dismissing;
    private ConfirmDialog DismissConfirm = default!;

    private static readonly List<string> WelcomePermissions = new() { PermissionIdentifier.ProcessApplications };

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
        Loading = false;
    }

    private async Task LoadAsync() {
        try {
            App = await Http.GetFromJsonAsync<MembershipApplicationDetailDTO>(
                $"/api/admin/membershipapplications/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private bool ShowRegisterCard => App?.RegisterHits != null && (App.RegisterHits.Count > 0 || App.HasPriorDeclinedApplication);

    private bool CanDismiss(ApplicationRegisterHitDTO hit) {
        if (hit.DismissedAt != null)
            return false;
        return App!.Status != ApplicationStatus.Approved && App.Status != ApplicationStatus.Rejected;
    }

    private async Task DismissHit(ApplicationRegisterHitDTO hit) {
        if (Dismissing || App == null)
            return;

        var confirmed = await DismissConfirm.ShowAsync();
        if (!confirmed)
            return;

        Dismissing = true;
        var dismissed = await Register.DismissHitAsync(App.Id, hit.Id);
        Dismissing = false;
        if (dismissed) {
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            await LoadAsync();
        }
    }

    private string RegisterKindLabel(ApplicantRegisterKind kind) {
        return ApplicantRegisterKindLabel.Of(I18n, kind);
    }

    private string MatchedFieldsText(RegisterMatchFields fields) {
        var labels = new List<string>();
        if (fields.HasFlag(RegisterMatchFields.FirstName))
            labels.Add(I18n[I18nKey.Ui.ApplicationDetail.MatchFirstName]);
        if (fields.HasFlag(RegisterMatchFields.LastName))
            labels.Add(I18n[I18nKey.Ui.ApplicationDetail.MatchLastName]);
        if (fields.HasFlag(RegisterMatchFields.DateOfBirth))
            labels.Add(I18n[I18nKey.Ui.ApplicationDetail.MatchDateOfBirth]);
        if (fields.HasFlag(RegisterMatchFields.Street))
            labels.Add(I18n[I18nKey.Ui.ApplicationDetail.MatchStreet]);
        if (fields.HasFlag(RegisterMatchFields.PostCodeCity))
            labels.Add(I18n[I18nKey.Ui.ApplicationDetail.MatchPostCodeCity]);
        if (fields.HasFlag(RegisterMatchFields.Email))
            labels.Add(I18n[I18nKey.Ui.ApplicationDetail.MatchEmail]);
        return string.Join(", ", labels);
    }

    private async Task SendWelcome() {
        if (Sending || App == null || WelcomeMemberNumber == null || WelcomeMemberNumber <= 0) {
            return;
        }
        Sending = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync("/api/admin/membershipapplications/welcome",
                new { Id = App.Id, MemberNumber = WelcomeMemberNumber.Value });
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.WelcomeMailSent);
                await LoadAsync();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Sending = false;
            StateHasChanged();
        }
    }

    private async Task OnDivisionSelected(AdministrativeDivisionDTO division) {
        SelectedDivision = division;
        SelectedChapterName = null;
        StateHasChanged();
        await LookupChapter(division.Id);
    }

    private async Task LookupChapter(Guid divisionId) {
        try {
            var resp = await Http.GetAsync($"/api/chapters/for-division/{divisionId}");
            if (resp.StatusCode == HttpStatusCode.NotFound) {
                SelectedChapterName = null;
                return;
            }
            resp.EnsureSuccessStatusCode();
            var chapter = await resp.Content.ReadFromJsonAsync<ChapterDTO>();
            SelectedChapterName = chapter?.Name;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            SelectedChapterName = null;
        } finally {
            StateHasChanged();
        }
    }

    private Task ConfirmDivision() {
        if (SelectedDivision == null) {
            return Task.CompletedTask;
        }
        return LinkDivision(new { Id = App!.Id, AdministrativeDivisionId = SelectedDivision.Id, NotInGermany = false });
    }

    private Task MarkNotInGermany() {
        return LinkDivision(new { Id = App!.Id, AdministrativeDivisionId = (Guid?)null, NotInGermany = true });
    }

    private async Task LinkDivision(object request) {
        if (Linking || App == null) {
            return;
        }
        Linking = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync("/api/admin/membershipapplications/link-division", request);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.DivisionLinked);
                SelectedDivision = null;
                SelectedChapterName = null;
                await LoadAsync();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Linking = false;
            StateHasChanged();
        }
    }

    private string ValuationLabel(SelectedValuation valuation) => valuation switch {
        SelectedValuation.MonthlyPayGroup => I18n[I18nKey.Ui.ApplicationDetail.ValuationMonthlyPay],
        SelectedValuation.OnePercentYearlyPay => I18n[I18nKey.Ui.ApplicationDetail.ValuationOnePercent],
        SelectedValuation.Underage => I18n[I18nKey.Ui.ApplicationDetail.ValuationUnderage],
        SelectedValuation.Reduced => I18n[I18nKey.Ui.ApplicationDetail.ValuationReduced],
        _ => I18n[I18nKey.Ui.ApplicationDetail.ValuationUnknown]
    };

    private string ApprovalMailText => App!.ApprovalMailState switch {
        ApprovalMailState.Sent => I18n[I18nParams.With(I18nKey.Ui.ApplicationDetail.ApprovalSent, ("date", App.ApprovalMailSentAt?.LocalDateTime.ToString("dd.MM.yyyy HH:mm") ?? ""))],
        ApprovalMailState.WaitingForReduction => I18n[I18nKey.Ui.ApplicationDetail.ApprovalWaitingReduction],
        ApprovalMailState.ManuallyResolved => I18n[I18nKey.Ui.ApplicationDetail.ApprovalManuallyResolved],
        ApprovalMailState.Failed => I18n[I18nKey.Ui.ApplicationDetail.ApprovalFailed],
        _ => I18n[I18nKey.Ui.ApplicationDetail.ApprovalWaitingSage]
    };

    private string ApprovalMailClass => App!.ApprovalMailState switch {
        ApprovalMailState.Sent => "alert-success",
        ApprovalMailState.ManuallyResolved => "alert-warning",
        ApprovalMailState.Failed => "alert-danger",
        _ => "alert-info"
    };

    private string DueStatusLabel(DueSelectionStatus status) => status switch {
        DueSelectionStatus.Pending => I18n[I18nKey.Ui.ApplicationDetail.DueStatusPending],
        DueSelectionStatus.Approved => I18n[I18nKey.Ui.ApplicationDetail.DueStatusApproved],
        DueSelectionStatus.Rejected => I18n[I18nKey.Ui.ApplicationDetail.DueStatusRejected],
        DueSelectionStatus.AutoApproved => I18n[I18nKey.Ui.ApplicationDetail.DueStatusAutoApproved],
        DueSelectionStatus.Forwarded => I18n[I18nKey.Ui.ApplicationDetail.DueStatusForwarded],
        _ => I18n[I18nKey.Ui.ApplicationDetail.DueStatusUnknown]
    };
}
