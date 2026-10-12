using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyMotions;

public partial class AssemblyMotionDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private AssemblyMotionPublicDTO? Motion;
    private AssemblyMotionDetailDTO? Member;
    private bool Loading = true;
    private bool Busy;
    private ConfirmDialog _joinDialog = default!;
    private ConfirmDialog _leaveDialog = default!;
    private ConfirmDialog _withdrawDialog = default!;
    private ConfirmDialog _deleteDialog = default!;

    private string BackHref => Motion == null ? "/Events" : $"/Events/{Motion.EventId}/Motions";

    private AssemblyMotionInquiryDTO? OpenInquiry => Member?.Inquiries.FirstOrDefault(i => i.ClosedAt == null);

    private bool ShowEditLockedHint => Member != null
        && Member.IsAuthor
        && !Member.CanEdit
        && IsPastDraftAndActive(Member.Status);

    private string ShareLink => $"{Navigation.BaseUri}AssemblyMotions/{Id}";

    private string CollectingProgressText => Member == null
        ? ""
        : I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.CollectingProgress,
            ("count", Member.Submitters.Count.ToString()),
            ("min", Member.MinSubmitters.ToString()))];

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Loading = true;
        Motion = null;
        Member = null;
        try {
            await AuthService.WaitForInitializationAsync();
            if (AuthService.IsAuthenticated) {
                var memberResponse = await Http.GetAsync($"/api/assemblymotions/motions/{Id}");
                if (memberResponse.IsSuccessStatusCode)
                    Apply(await memberResponse.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>());
            }

            if (Motion == null) {
                var publicResponse = await Http.GetAsync($"/api/assemblymotions/motions/{Id}/public");
                if (publicResponse.IsSuccessStatusCode)
                    Motion = await publicResponse.Content.ReadFromJsonAsync<AssemblyMotionPublicDTO>();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private void Apply(AssemblyMotionDetailDTO? detail) {
        Member = detail;
        Motion = detail;
    }

    private static bool IsPastDraftAndActive(AssemblyMotionStatus status)
        => status == AssemblyMotionStatus.CollectingCoSubmitters
            || status == AssemblyMotionStatus.Submitted
            || status == AssemblyMotionStatus.Admitted
            || status == AssemblyMotionStatus.FormallyInsufficient;

    private Task Release() => RunActionAsync("release", I18nKey.Ui.AssemblyMotion.ToastReleased);

    private async Task Join() {
        if (Motion == null)
            return;

        var message = I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.JoinConfirmMessage, ("title", Motion.Title))];
        if (await _joinDialog.ShowAsync(message))
            await RunActionAsync("join", I18nKey.Ui.AssemblyMotion.ToastJoined);
    }

    private async Task Leave() {
        if (await _leaveDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.LeaveConfirmMessage]))
            await RunActionAsync("leave", I18nKey.Ui.AssemblyMotion.ToastLeft);
    }

    private async Task Withdraw() {
        if (await _withdrawDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.WithdrawConfirmMessage]))
            await RunActionAsync("withdraw", I18nKey.Ui.AssemblyMotion.ToastWithdrawn);
    }

    private async Task DeleteDraft() {
        if (!await _deleteDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.DeleteConfirmMessage]))
            return;

        var backHref = BackHref;
        Busy = true;
        try {
            var response = await Http.DeleteAsync($"/api/assemblymotions/motions/{Id}");
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastDeleted);
                Navigation.NavigateTo(backHref);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Busy = false;
    }

    private async Task RunActionAsync(string action, string toastKey) {
        var backHref = BackHref;
        Busy = true;
        try {
            var response = await Http.PostAsJsonAsync($"/api/assemblymotions/motions/{Id}/{action}", new { });
            if (response.StatusCode == HttpStatusCode.NoContent) {
                Navigation.NavigateTo(backHref);
            } else if (response.IsSuccessStatusCode) {
                Apply(await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>());
                ToastService.ToastKey(toastKey);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Busy = false;
    }
}
