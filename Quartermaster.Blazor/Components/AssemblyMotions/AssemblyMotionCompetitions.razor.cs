using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionCompetitions {
    private const int SearchPageSize = 100;

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required AssemblyMotionDetailDTO Motion { get; set; }

    [Parameter]
    public List<AssemblyMotionTypeDTO> Types { get; set; } = [];

    [Parameter]
    public bool AsCommission { get; set; }

    [Parameter]
    public EventCallback<AssemblyMotionDetailDTO> OnChanged { get; set; }

    private ConfirmDialog RemoveDialog = default!;
    private List<AssemblyMotionPublicListItemDTO> Candidates = [];
    private bool Busy;
    private bool Searching;
    private bool Searched;
    private int FlagRenderKey;
    private string Search = "";
    private bool TooManyResults;

    private bool IsVisible {
        get {
            if (Motion.CompetitionLinks.Count > 0)
                return true;
            if (Motion.IsCompetingMotion)
                return true;
            return CanDeclare || CanSetFlag;
        }
    }

    private bool CanSetFlag {
        get {
            if (AsCommission)
                return false;
            if (!Motion.IsAuthor)
                return false;
            return Motion.CanEdit;
        }
    }

    private bool CanClearFlag => AsCommission && Motion.IsCompetingMotion;

    private bool CanDeclare {
        get {
            if (!CanDeclareFrom(Motion.Status))
                return false;
            if (AsCommission)
                return Motion.Status != AssemblyMotionStatus.Draft;
            if (Motion.Status == AssemblyMotionStatus.Draft)
                return Motion.IsAuthor;
            return Motion.IsSubmitter;
        }
    }

    private string FlagExplanation {
        get {
            var type = Types.FirstOrDefault(t => t.Id == Motion.TypeId);
            if (type == null)
                return "";

            var deadline = type.CompetingDeadline;
            if (deadline == null)
                return I18n[I18nKey.Ui.AssemblyMotion.CompetingFlagNoDeadline];

            return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.CompetingFlagDeadline, ("deadline", AssemblyMotionDisplay.Berlin(deadline.Value)))];
        }
    }

    private static bool CanDeclareFrom(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.Draft)
            return true;
        if (status == AssemblyMotionStatus.CollectingCoSubmitters)
            return true;
        return IsPickable(status);
    }

    private static bool IsPickable(AssemblyMotionStatus status) {
        return status == AssemblyMotionStatus.Submitted || status == AssemblyMotionStatus.Admitted;
    }

    private void OnSearchInput(ChangeEventArgs e) {
        Search = e.Value?.ToString() ?? "";
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter")
            await RunSearch();
    }

    private async Task RunSearch() {
        Searching = true;
        try {
            var query = $"?Page=1&PageSize={SearchPageSize}";
            if (!string.IsNullOrWhiteSpace(Search))
                query += $"&Search={Uri.EscapeDataString(Search.Trim())}";

            var response = await Http.GetAsync($"/api/assemblymotions/portals/{Motion.PortalId}/motions{query}");
            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<AssemblyMotionPublicListResponse>();
                var linked = Motion.CompetitionLinks
                    .Select(l => l.OtherMotionId)
                    .ToHashSet();
                Candidates = (result?.Items ?? [])
                    .Where(i => i.Id != Motion.Id && IsPickable(i.Status) && !linked.Contains(i.Id))
                    .ToList();
                Searched = true;
                TooManyResults = result != null && result.TotalCount > result.Items.Count;
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Searching = false;
        }
    }

    private async Task Declare(Guid otherMotionId) {
        var request = new AssemblyMotionCompetitionDeclareRequest {
            MotionId = Motion.Id,
            OtherMotionId = otherMotionId
        };
        if (!await SendAsync(() => Http.PostAsJsonAsync($"/api/assemblymotions/motions/{Motion.Id}/competitions", request), I18nKey.Ui.AssemblyMotion.ToastCompetitionDeclared))
            return;

        Candidates = Candidates
            .Where(c => c.Id != otherMotionId)
            .ToList();
    }

    private async Task Remove(Guid competitionId) {
        if (!await RemoveDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.CompetitionRemoveMessage]))
            return;

        // The server answers with the declaring motion, which may be the other side of the link, so this motion is reloaded.
        Busy = true;
        try {
            var response = await Http.DeleteAsync($"/api/assemblymotions/competitions/{competitionId}");
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastCompetitionRemoved);
            var reload = await Http.GetAsync($"/api/assemblymotions/motions/{Motion.Id}");
            if (!reload.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(reload);
                return;
            }

            var detail = await reload.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
            if (detail != null)
                await OnChanged.InvokeAsync(detail);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task OnFlagChanged(ChangeEventArgs e) {
        await SetFlag(e.Value is true);
    }

    private Task ClearFlag() {
        return SetFlag(false);
    }

    private async Task SetFlag(bool value) {
        var request = new AssemblyMotionCompetingFlagRequest {
            Id = Motion.Id,
            Value = value
        };
        if (!await SendAsync(() => Http.PutAsJsonAsync($"/api/assemblymotions/motions/{Motion.Id}/competing-flag", request), I18nKey.Ui.AssemblyMotion.ToastCompetingFlagChanged))
            FlagRenderKey++;
    }

    private async Task<bool> SendAsync(Func<Task<HttpResponseMessage>> send, string toastKey) {
        Busy = true;
        var succeeded = false;
        try {
            var response = await send();
            if (response.IsSuccessStatusCode) {
                var detail = await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
                ToastService.ToastKey(toastKey);
                succeeded = true;
                if (detail != null)
                    await OnChanged.InvokeAsync(detail);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
        return succeeded;
    }
}
