using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Api;

/// <summary>
/// Typed client for the <c>/api/meetings/*</c> endpoint family. Hides URL construction
/// and generic response-type plumbing from page code. Network failures surface as
/// <see cref="HttpRequestException"/> for the caller's existing try/catch + ToastService.Error
/// flow; HTTP error status codes are returned via the response object when callers need to
/// branch on them.
/// </summary>
public class MeetingsApi {
    private readonly HttpClient _http;

    public MeetingsApi(HttpClient http) {
        _http = http;
    }

    public async Task<MeetingDetailDTO?> GetAsync(Guid id) {
        var response = await _http.GetAsync($"/api/meetings/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MeetingDetailDTO>();
    }

    public async Task<MeetingListResponse?> ListAsync(MeetingListRequest request) {
        var url = $"/api/meetings?page={request.Page}&pageSize={request.PageSize}";
        if (request.ChapterId.HasValue)
            url += $"&chapterId={request.ChapterId.Value}";
        if (request.Status.HasValue)
            url += $"&status={(int)request.Status.Value}";
        if (request.Visibility.HasValue)
            url += $"&visibility={(int)request.Visibility.Value}";
        if (request.DateFrom.HasValue)
            url += $"&dateFrom={Uri.EscapeDataString(request.DateFrom.Value.ToString("o"))}";
        if (request.DateTo.HasValue)
            url += $"&dateTo={Uri.EscapeDataString(request.DateTo.Value.ToString("o"))}";
        return await _http.GetFromJsonAsync<MeetingListResponse>(url);
    }

    /// <summary>
    /// Overload for callers that already have the query string composed.
    /// Lets the list page keep its incremental URL-building style without forcing
    /// it to round-trip through <see cref="MeetingListRequest"/>.
    /// </summary>
    public async Task<MeetingListResponse?> ListAsync(string queryString) {
        var url = queryString.StartsWith('?')
            ? $"/api/meetings{queryString}"
            : $"/api/meetings?{queryString}";
        return await _http.GetFromJsonAsync<MeetingListResponse>(url);
    }

    public async Task<HttpResponseMessage> CreateAsync(MeetingCreateRequest request) {
        return await _http.PostAsJsonAsync("/api/meetings", request);
    }

    public async Task<HttpResponseMessage> UpdateAsync(MeetingUpdateRequest request) {
        return await _http.PutAsJsonAsync($"/api/meetings/{request.Id}", request);
    }

    public async Task<HttpResponseMessage> UpdateStatusAsync(MeetingStatusUpdateRequest request) {
        return await _http.PutAsJsonAsync($"/api/meetings/{request.Id}/status", request);
    }

    public async Task<HttpResponseMessage> DeleteAsync(Guid id) {
        return await _http.DeleteAsync($"/api/meetings/{id}");
    }

    public async Task<string> GetProtocolHtmlAsync(Guid id, bool draft) {
        var draftSuffix = draft ? "&draft=true" : "";
        return await _http.GetStringAsync($"/api/meetings/{id}/protocol?format=html{draftSuffix}");
    }

    public async Task<HttpResponseMessage> AddAgendaItemAsync(AgendaItemCreateRequest request) {
        return await _http.PostAsJsonAsync($"/api/meetings/{request.MeetingId}/agenda", request);
    }

    public async Task<HttpResponseMessage> UpdateAgendaItemAsync(AgendaItemUpdateRequest request) {
        return await _http.PutAsJsonAsync(
            $"/api/meetings/{request.MeetingId}/agenda/{request.ItemId}", request);
    }

    public async Task<HttpResponseMessage> MoveAgendaItemAsync(AgendaItemMoveRequest request) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{request.MeetingId}/agenda/{request.ItemId}/move", request);
    }

    public async Task<HttpResponseMessage> ReorderAgendaItemAsync(AgendaItemReorderRequest request) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{request.MeetingId}/agenda/{request.ItemId}/reorder", request);
    }

    public async Task<HttpResponseMessage> UpdateNotesAsync(AgendaItemNotesRequest request) {
        return await _http.PutAsJsonAsync(
            $"/api/meetings/{request.MeetingId}/agenda/{request.ItemId}/notes", request);
    }

    public async Task<HttpResponseMessage> DeleteAgendaItemAsync(Guid meetingId, Guid itemId) {
        return await _http.DeleteAsync($"/api/meetings/{meetingId}/agenda/{itemId}");
    }

    public async Task<HttpResponseMessage> StartAgendaItemAsync(Guid meetingId, Guid itemId) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/start", new { });
    }

    public async Task<HttpResponseMessage> CompleteAgendaItemAsync(Guid meetingId, Guid itemId) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/complete", new { });
    }

    public async Task<HttpResponseMessage> ReopenAgendaItemAsync(Guid meetingId, Guid itemId) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/reopen", new { });
    }

    public async Task<HttpResponseMessage> VoteAgendaItemAsync(AgendaItemVoteRequest request) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{request.MeetingId}/agenda/{request.ItemId}/vote", request);
    }

    public async Task<HttpResponseMessage> CloseVoteAgendaItemAsync(Guid meetingId, Guid itemId) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/close-vote", new { });
    }

    /// <summary>
    /// Body shape matches the server's <c>AgendaItemImportMotionsRequest</c>
    /// (<c>MeetingId</c> + optional <c>ParentId</c>). That request type lives in
    /// <c>Quartermaster.Server</c>, so the Blazor client posts an anonymous-object
    /// equivalent rather than reaching across the project boundary.
    /// </summary>
    public async Task<HttpResponseMessage> ImportMotionsAsync(Guid meetingId, Guid? parentId) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/import-motions",
            new { MeetingId = meetingId, ParentId = parentId });
    }

    public async Task<HttpResponseMessage> SetPresenceAsync(AgendaItemPresenceRequest request) {
        return await _http.PostAsJsonAsync(
            $"/api/meetings/{request.MeetingId}/agenda/{request.ItemId}/presence", request);
    }
}
