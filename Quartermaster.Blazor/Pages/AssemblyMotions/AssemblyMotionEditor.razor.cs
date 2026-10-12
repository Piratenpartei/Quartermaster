using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Http;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyMotions;

public partial class AssemblyMotionEditor {
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
    public Guid EventId { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private AssemblyMotionPortalDTO? Portal;
    private AssemblyMotionPortalContextDTO? Context;
    private AssemblyMotionDetailDTO? Detail;
    private ConfirmDialog _revisionDialog = default!;
    private ConfirmDialog _reloadDialog = default!;
    private bool Loading = true;
    private bool Saving;
    private string? Conflict;
    private Guid motionId;
    private int revision;

    private string TypeId = "";
    private string Title = "";
    private string Section = "";
    private string TextMarkdown = "";
    private string ReasonMarkdown = "";
    private string Summary = "";
    private string Keywords = "";
    private string PriorDiscussion = "";

    private bool IsExisting => Id != Guid.Empty;

    private bool IsDraftOrNew => Detail == null || Detail.Status == AssemblyMotionStatus.Draft;

    private bool IsRevisionEdit => Detail != null && Detail.Status != AssemblyMotionStatus.Draft;

    private bool TypeLocked => Detail != null && !string.IsNullOrEmpty(Detail.DisplayNumber);

    private AssemblyMotionTypeDTO? SelectedType => Portal?.Types.FirstOrDefault(t => t.Id.ToString() == TypeId);

    private string BackHref => motionId != Guid.Empty
        ? $"/AssemblyMotions/{motionId}"
        : $"/Events/{EventId}/Motions";

    private string RevisionWarning => SelectedType != null && SelectedType.MinSubmitters <= 1
        ? I18n[I18nKey.Ui.AssemblyMotion.RevisionWarningSingle]
        : I18n[I18nKey.Ui.AssemblyMotion.RevisionWarningMulti];

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    private async Task ReloadAsync() {
        if (await _reloadDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.ReloadConfirmMessage]))
            await LoadAsync();
    }

    private async Task LoadAsync() {
        Loading = true;
        Conflict = null;
        try {
            await AuthService.WaitForInitializationAsync();
            if (!AuthService.IsAuthenticated) {
                Loading = false;
                return;
            }

            var eventId = EventId;
            if (IsExisting) {
                motionId = Id;
                var detailResponse = await Http.GetAsync($"/api/assemblymotions/motions/{Id}");
                Detail = detailResponse.IsSuccessStatusCode ? await detailResponse.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>() : null;
                if (Detail != null) {
                    eventId = Detail.EventId;
                    Fill(Detail);
                }
            }

            if (eventId != Guid.Empty) {
                var portalResponse = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{eventId}");
                Portal = portalResponse.IsSuccessStatusCode ? await portalResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            }

            if (Portal != null && !IsExisting) {
                Context = await Http.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{Portal.Id}/context");
                if (TypeId == "" && Portal.Types.Count > 0)
                    TypeId = Portal.Types[0].Id.ToString();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private void Fill(AssemblyMotionDetailDTO detail) {
        TypeId = detail.TypeId.ToString();
        Title = detail.Title;
        Section = detail.Section ?? "";
        TextMarkdown = detail.TextMarkdown;
        ReasonMarkdown = detail.ReasonMarkdown;
        Summary = detail.Summary ?? "";
        Keywords = detail.Keywords ?? "";
        PriorDiscussion = detail.PriorDiscussion ?? "";
        revision = detail.Revision;
    }

    private void OnTypeChanged(string value) {
        TypeId = value;
    }

    private async Task SaveDraft() {
        if (IsRevisionEdit && !await _revisionDialog.ShowAsync(RevisionWarning))
            return;

        Saving = true;
        var saved = await SaveAsync();
        Saving = false;
        if (saved) {
            ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastSaved);
            Navigation.NavigateTo($"/AssemblyMotions/{motionId}");
        }
    }

    private async Task Release() {
        Saving = true;
        try {
            if (!await SaveAsync())
                return;

            var response = await Http.PostAsJsonAsync($"/api/assemblymotions/motions/{motionId}/release", new { });
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastReleased);
                Navigation.NavigateTo($"/AssemblyMotions/{motionId}");
            } else {
                await ShowErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Saving = false;
        }
    }

    private async Task<bool> SaveAsync() {
        if (Portal == null || !Guid.TryParse(TypeId, out var typeId))
            return false;

        Conflict = null;
        var request = new AssemblyMotionSaveRequest {
            PortalId = Portal.Id,
            Id = motionId,
            TypeId = typeId,
            Title = Title,
            Section = NullIfBlank(Section),
            TextMarkdown = TextMarkdown,
            ReasonMarkdown = ReasonMarkdown,
            Summary = NullIfBlank(Summary),
            Keywords = NullIfBlank(Keywords),
            PriorDiscussion = NullIfBlank(PriorDiscussion),
            BaseRevision = revision
        };

        try {
            var response = motionId == Guid.Empty
                ? await Http.PostAsJsonAsync($"/api/assemblymotions/portals/{Portal.Id}/motions", request)
                : await Http.PutAsJsonAsync($"/api/assemblymotions/motions/{motionId}", request);
            if (response.IsSuccessStatusCode) {
                var saved = await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
                if (saved == null)
                    return false;

                motionId = saved.Id;
                revision = saved.Revision;
                Detail = saved;
                return true;
            }

            await ShowErrorAsync(response);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        return false;
    }

    private async Task ShowErrorAsync(HttpResponseMessage response) {
        var messages = await ApiErrorHelper.GetTranslatedErrorsAsync(response, I18n);
        var revisionConflict = I18n[I18nKey.Error.AssemblyMotion.RevisionConflict];
        if (response.StatusCode == HttpStatusCode.Conflict && messages.Contains(revisionConflict))
            Conflict = revisionConflict;
        else if (messages.Count > 0)
            ToastService.Error(string.Join("\n", messages));
        else
            ToastService.ErrorKey(I18nKey.Ui.AssemblyMotion.SaveFailed);
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
