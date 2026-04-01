using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Members;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterOfficerAdd {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid ChapterId { get; set; }

    private string SearchQuery { get; set; } = "";
    private bool SearchLoading;
    private MemberSearchResponse? SearchResults;
    private MemberDTO? SelectedMember;
    private Guid? SelectedMemberId;
    private int SelectedRole { get; set; } = 6;
    private bool Submitting;
    private CancellationTokenSource? _debounceTokenSource;

    private async Task OnSearchKeyUp() {
        _debounceTokenSource?.Cancel();
        _debounceTokenSource = new CancellationTokenSource();
        var token = _debounceTokenSource.Token;

        if (SearchQuery.Length < 3) {
            SearchResults = null;
            StateHasChanged();
            return;
        }

        try {
            await Task.Delay(300, token);
            await SearchMembers();
        } catch (TaskCanceledException) { }
    }

    private async Task SearchMembers() {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        SearchLoading = true;
        StateHasChanged();

        try {
            SearchResults = await Http.GetFromJsonAsync<MemberSearchResponse>(
                $"/api/members?query={Uri.EscapeDataString(SearchQuery)}&page=1&pageSize=10");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        SearchLoading = false;
        StateHasChanged();
    }

    private void SelectMember(MemberDTO member) {
        SelectedMember = member;
        SelectedMemberId = member.Id;
        StateHasChanged();
    }

    private async Task AddOfficer() {
        if (SelectedMemberId == null)
            return;

        Submitting = true;
        StateHasChanged();

        try {
            await Http.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
                MemberId = SelectedMemberId.Value,
                ChapterId = ChapterId,
                AssociateType = SelectedRole
            });

            Navigation.NavigateTo($"/Administration/Chapters/{ChapterId}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Submitting = false;
            StateHasChanged();
        }
    }
}
