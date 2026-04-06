using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class MotionPicker {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public string? InitialTitle { get; set; }

    [Parameter]
    public string ChapterId { get; set; } = "";

    [Parameter]
    public bool Disabled { get; set; }

    private List<MotionDTO>? Motions;
    private string SearchText = "";
    private bool ShowDropdown;
    private bool Loading;
    private string? _loadedForChapterId;

    private List<MotionDTO> FilteredMotions {
        get {
            if (Motions == null)
                return new();
            if (string.IsNullOrWhiteSpace(SearchText))
                return Motions;
            return Motions.Where(m =>
                m.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || m.AuthorName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    protected override void OnParametersSet() {
        if (!string.IsNullOrEmpty(InitialTitle) && string.IsNullOrEmpty(SearchText))
            SearchText = InitialTitle;
    }

    private async Task EnsureLoaded() {
        if (string.IsNullOrEmpty(ChapterId))
            return;
        if (_loadedForChapterId == ChapterId && Motions != null)
            return;
        Loading = true;
        StateHasChanged();
        try {
            var url = $"/api/motions?chapterId={ChapterId}&approvalStatus=0&pageSize=100";
            var resp = await Http.GetFromJsonAsync<MotionListResponse>(url);
            Motions = resp?.Items ?? new();
            _loadedForChapterId = ChapterId;
        } catch {
            Motions = new();
        }
        Loading = false;
        StateHasChanged();
    }

    private async Task OnFocus() {
        ShowDropdown = true;
        await EnsureLoaded();
    }

    private async Task OnSearchInput(ChangeEventArgs e) {
        SearchText = e.Value?.ToString() ?? "";
        ShowDropdown = true;
        await EnsureLoaded();
        StateHasChanged();
    }

    private async Task SelectMotion(MotionDTO motion) {
        Value = motion.Id.ToString();
        SearchText = motion.Title;
        ShowDropdown = false;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task Clear() {
        Value = "";
        SearchText = "";
        ShowDropdown = false;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task ScheduleClose() {
        await Task.Delay(200);
        ShowDropdown = false;
        StateHasChanged();
    }
}
