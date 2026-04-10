using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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

    private async Task CastVote(Guid userId, int vote) {
        try {
            await Http.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
                MotionId = Id,
                UserId = userId,
                Vote = vote
            });

            await LoadMotion();
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SetStatus(int status) {
        try {
            await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
                MotionId = Id,
                ApprovalStatus = status
            });

            ToastService.Toast("Status aktualisiert.", "success");
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

            ToastService.Toast("Als umgesetzt markiert.", "success");
            await LoadMotion();
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private MotionVoteDTO? GetVoteForOfficer(Guid userId)
        => Motion?.Votes.FirstOrDefault(v => v.UserId == userId);

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
