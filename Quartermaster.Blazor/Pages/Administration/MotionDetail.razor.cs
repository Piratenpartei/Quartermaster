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
        } catch (HttpRequestException) { }
        Loading = false;
    }

    private async Task CastVote(Guid userId, int vote) {
        await Http.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = Id,
            UserId = userId,
            Vote = vote
        });

        await LoadMotion();
        StateHasChanged();
    }

    private async Task SetStatus(int status) {
        await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = Id,
            ApprovalStatus = status
        });

        ToastService.Toast("Status aktualisiert.", "success");
        await LoadMotion();
        StateHasChanged();
    }

    private async Task MarkRealized() {
        await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = Id,
            IsRealized = true
        });

        ToastService.Toast("Als umgesetzt markiert.", "success");
        await LoadMotion();
        StateHasChanged();
    }

    private MotionVoteDTO? GetVoteForOfficer(Guid userId)
        => Motion?.Votes.FirstOrDefault(v => v.UserId == userId);

    private static string ApprovalLabel(int status) => status switch {
        0 => "Ausstehend",
        1 => "Genehmigt",
        2 => "Abgelehnt",
        3 => "Formal abgelehnt",
        4 => "Ohne Beschluss",
        _ => "Unbekannt"
    };

    private static string ApprovalBadgeClass(int status) => status switch {
        0 => "border-warning text-warning-emphasis",
        1 => "border-success text-success-emphasis",
        2 => "border-danger text-danger-emphasis",
        3 => "border-secondary text-secondary-emphasis",
        4 => "border-secondary text-secondary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };

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
