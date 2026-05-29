using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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

    [Parameter]
    public Guid Id { get; set; }

    private MotionDetailDTO? Motion;
    private bool Loading = true;
    private bool TogglingPublic;

    protected override async Task OnInitializedAsync() {
        await LoadMotion();
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

    private static string OfficerRoleLabel(string role) => role switch {
        "Captain" => "Vorsitzender",
        "FirstOfficer" => "Stellv. Vorsitzender",
        "Quartermaster" => "Quartiermeister",
        "Treasurer" => "Schatzmeister",
        "ViceTreasurer" => "Stellv. Schatzmeister",
        "PoliticalDirector" => "Pol. Geschäftsführer",
        "Member" => "Beisitzer",
        _ => role
    };
}
