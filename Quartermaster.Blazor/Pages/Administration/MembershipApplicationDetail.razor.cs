using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MembershipApplicationDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MembershipApplicationDetailDTO? App;
    private bool Loading = true;
    private int? WelcomeMemberNumber;
    private bool Sending;
    private AdministrativeDivisionDTO? SelectedDivision;
    private string? SelectedChapterName;
    private bool Linking;

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

    private string DueStatusLabel(DueSelectionStatus status) => status switch {
        DueSelectionStatus.Pending => I18n[I18nKey.Ui.ApplicationDetail.DueStatusPending],
        DueSelectionStatus.Approved => I18n[I18nKey.Ui.ApplicationDetail.DueStatusApproved],
        DueSelectionStatus.Rejected => I18n[I18nKey.Ui.ApplicationDetail.DueStatusRejected],
        DueSelectionStatus.AutoApproved => I18n[I18nKey.Ui.ApplicationDetail.DueStatusAutoApproved],
        _ => I18n[I18nKey.Ui.ApplicationDetail.DueStatusUnknown]
    };
}
