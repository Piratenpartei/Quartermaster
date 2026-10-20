using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.AssemblyMotions;
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

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private AssemblyMotionPublicDTO? Motion;
    private AssemblyMotionDetailDTO? Member;
    private AssemblyMotionPortalDTO? Portal;
    private AssemblyMotionPortalContextDTO? Context;
    private bool Loading = true;
    private bool Busy;
    private bool CanShare;
    private ConfirmDialog ReleaseDialog = default!;
    private ConfirmDialog JoinDialog = default!;
    private ConfirmDialog LeaveDialog = default!;
    private ConfirmDialog WithdrawDialog = default!;
    private ConfirmDialog DeleteDialog = default!;

    private string BackHref => Motion == null ? "/Events" : $"/Events/{Motion.EventId}/Motions";

    private AssemblyMotionInquiryDTO? OpenInquiry => Member?.Inquiries.FirstOrDefault(i => i.ClosedAt == null);

    private List<AssemblyMotionTypeDTO> PortalTypes => Portal?.Types ?? [];

    private bool ShowUpvote {
        get {
            if (Motion == null)
                return false;
            if (Motion.UpvoteCount != null)
                return true;
            if (Member == null)
                return false;
            return Member.CanUpvote || Member.HasUpvoted;
        }
    }

    private bool ShowUpvoteToggle {
        get {
            if (Member == null)
                return false;
            if (Member.CanUpvote)
                return true;
            if (!Member.HasUpvoted || Portal == null)
                return false;
            return AssemblyMotionDisplay.IsPrioritizationOpen(Portal);
        }
    }

    private string ShareLink => $"{Navigation.BaseUri}AssemblyMotions/{Id}";

    private string UpvoteCountText {
        get {
            if (Motion?.UpvoteCount == null)
                return "";

            var count = Motion.UpvoteCount.Value;
            if (count == 1)
                return I18n[I18nKey.Ui.AssemblyMotion.UpvoteCountTextOne];

            return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.UpvoteCountText, ("count", count.ToString()))];
        }
    }

    private string CollectingProgressText => Member == null
        ? ""
        : I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.CollectingProgress,
            ("count", Member.Submitters.Count.ToString()),
            ("min", Member.MinSubmitters.ToString()))];

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender)
            return;
        CanShare = await JS.InvokeAsync<bool>("CanShare");
        if (CanShare)
            StateHasChanged();
    }

    private async Task CopyShareLinkAsync() {
        try {
            if (await JS.InvokeAsync<bool>("CopyToClipboard", ShareLink))
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastLinkCopied);
        } catch (JSException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ShareAsync() {
        try {
            await JS.InvokeVoidAsync("ShareLink", Motion?.Title ?? "", ShareLink);
        } catch (JSException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task LoadAsync() {
        Loading = true;
        Motion = null;
        Member = null;
        Portal = null;
        Context = null;
        try {
            await AuthService.WaitForInitializationAsync();
            if (AuthService.IsAuthenticated) {
                var memberResponse = await Http.GetAsync($"/api/assemblymotions/motions/{Id}");
                if (memberResponse.IsSuccessStatusCode)
                    Apply(await memberResponse.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>());
            }

            if (Member != null) {
                var portalResponse = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{Member.EventId}");
                if (portalResponse.IsSuccessStatusCode)
                    Portal = await portalResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>();
            }

            if (Motion == null) {
                var publicResponse = await Http.GetAsync($"/api/assemblymotions/motions/{Id}/public");
                if (publicResponse.IsSuccessStatusCode)
                    Motion = await publicResponse.Content.ReadFromJsonAsync<AssemblyMotionPublicDTO>();
            }

            if (Member == null && Motion != null && Motion.Status == AssemblyMotionStatus.CollectingCoSubmitters) {
                var contextResponse = await Http.GetAsync($"/api/assemblymotions/portals/{Motion.PortalId}/context");
                if (contextResponse.IsSuccessStatusCode)
                    Context = await contextResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalContextDTO>();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private void Apply(AssemblyMotionDetailDTO? detail) {
        Member = detail;
        Motion = detail;
    }

    private bool ShowEditLockedHint() {
        if (Member == null)
            return false;
        if (!Member.IsAuthor)
            return false;
        if (Member.CanEdit)
            return false;
        return IsPastDraftAndActive(Member.Status);
    }

    private static bool IsPastDraftAndActive(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.CollectingCoSubmitters)
            return true;
        if (status == AssemblyMotionStatus.Submitted)
            return true;
        if (status == AssemblyMotionStatus.Admitted)
            return true;
        return status == AssemblyMotionStatus.FormallyInsufficient;
    }

    private async Task Release() {
        if (await ReleaseDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.ReleaseConfirmMessage]))
            await RunActionAsync("release", I18nKey.Ui.AssemblyMotion.ToastReleased);
    }

    private async Task Join() {
        if (Motion == null)
            return;

        var message = I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.JoinConfirmMessage, ("title", Motion.Title))];
        if (await JoinDialog.ShowAsync(message))
            await RunActionAsync("join", I18nKey.Ui.AssemblyMotion.ToastJoined);
    }

    private async Task Leave() {
        if (await LeaveDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.LeaveConfirmMessage]))
            await RunActionAsync("leave", I18nKey.Ui.AssemblyMotion.ToastLeft);
    }

    private async Task Withdraw() {
        if (await WithdrawDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.WithdrawConfirmMessage]))
            await RunActionAsync("withdraw", I18nKey.Ui.AssemblyMotion.ToastWithdrawn);
    }

    private async Task DeleteDraft() {
        if (!await DeleteDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.DeleteConfirmMessage]))
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
        } finally {
            Busy = false;
        }
    }

    private async Task AddUpvote() {
        await SendUpvoteAsync(() => Http.PostAsJsonAsync($"/api/assemblymotions/motions/{Id}/upvote", new { }), I18nKey.Ui.AssemblyMotion.ToastUpvoted);
    }

    private async Task RemoveUpvote() {
        await SendUpvoteAsync(() => Http.DeleteAsync($"/api/assemblymotions/motions/{Id}/upvote"), I18nKey.Ui.AssemblyMotion.ToastUpvoteRemoved);
    }

    private async Task SendUpvoteAsync(Func<Task<HttpResponseMessage>> send, string toastKey) {
        Busy = true;
        try {
            var response = await send();
            if (response.IsSuccessStatusCode) {
                Apply(await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>());
                ToastService.ToastKey(toastKey);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
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
        } finally {
            Busy = false;
        }
    }
}
