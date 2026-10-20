using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.AssemblyMotions;
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
    private ConfirmDialog RevisionDialog = default!;
    private ConfirmDialog ReloadDialog = default!;
    private ConfirmDialog ReleaseDialog = default!;
    private bool Loading = true;
    private bool Saving;
    private string? Conflict;
    private Guid motionId;
    private int revision;

    private AssemblyMotionFieldValues Fields = new();

    private bool IsExisting => Id != Guid.Empty;

    private bool IsDraftOrNew => Detail == null || Detail.Status == AssemblyMotionStatus.Draft;

    private bool IsRevisionEdit => Detail != null && Detail.Status != AssemblyMotionStatus.Draft;

    private bool TypeLocked => Detail != null && !string.IsNullOrEmpty(Detail.DisplayNumber);

    private AssemblyMotionTypeDTO? SelectedType => Portal?.Types.FirstOrDefault(t => t.Id.ToString() == Fields.TypeId);

    private string BackHref => motionId != Guid.Empty
        ? $"/AssemblyMotions/{motionId}"
        : $"/Events/{EventId}/Motions";

    private string RevisionWarning {
        get {
            if (Detail == null)
                return "";
            if (SelectedType != null && SelectedType.MinSubmitters <= 1)
                return I18n[I18nKey.Ui.AssemblyMotion.RevisionWarningSingle];

            var hasNumber = !string.IsNullOrEmpty(Detail.DisplayNumber);
            var coSubmitterCount = Detail.Submitters.Count(s => !s.IsAuthor);
            if (coSubmitterCount == 0)
                return I18n[hasNumber ? I18nKey.Ui.AssemblyMotion.RevisionWarningMultiAlone : I18nKey.Ui.AssemblyMotion.RevisionWarningUnnumberedAlone];

            if (!hasNumber)
                return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.RevisionWarningUnnumberedMulti,
                    ("deadline", AssemblyMotionDisplay.Deadline(I18n, Detail.EffectiveDeadline)))];

            if (coSubmitterCount == 1)
                return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.RevisionWarningMultiOne, ("deadline", AssemblyMotionDisplay.Deadline(I18n, Detail.EffectiveDeadline)))];

            return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.RevisionWarningMulti,
                ("count", coSubmitterCount.ToString()),
                ("deadline", AssemblyMotionDisplay.Deadline(I18n, Detail.EffectiveDeadline)))];
        }
    }

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    private async Task ReloadAsync() {
        if (await ReloadDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.ReloadConfirmMessage]))
            await LoadAsync();
    }

    private async Task LoadAsync() {
        Loading = true;
        Conflict = null;
        try {
            await AuthService.WaitForInitializationAsync();
            if (!AuthService.IsAuthenticated)
                return;

            var eventId = EventId;
            if (IsExisting) {
                motionId = Id;
                var detailResponse = await Http.GetAsync($"/api/assemblymotions/motions/{Id}");
                Detail = detailResponse.IsSuccessStatusCode ? await detailResponse.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>() : null;
                if (Detail != null) {
                    eventId = Detail.EventId;
                    Fields.Fill(Detail);
                    revision = Detail.Revision;
                }
            }

            if (eventId != Guid.Empty) {
                var portalResponse = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{eventId}");
                Portal = portalResponse.IsSuccessStatusCode ? await portalResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            }

            if (Portal != null && !IsExisting) {
                Context = await Http.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{Portal.Id}/context");
                if (Fields.TypeId == "" && Portal.Types.Count > 0)
                    Fields.ChangeType(Portal.Types[0].Id.ToString(), Portal.Types);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private async Task SaveDraft() {
        if (IsRevisionEdit && Fields.HasBindingChange(Detail!) && !await RevisionDialog.ShowAsync(RevisionWarning))
            return;

        Saving = true;
        bool saved;
        try {
            saved = await SaveAsync();
        } finally {
            Saving = false;
        }
        if (saved) {
            ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastSaved);
            Navigation.NavigateTo($"/AssemblyMotions/{motionId}");
        }
    }

    private async Task Release() {
        if (!await ReleaseDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.ReleaseConfirmMessage]))
            return;

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
        if (Portal == null)
            return false;

        Conflict = null;
        var request = new AssemblyMotionSaveRequest {
            PortalId = Portal.Id,
            Id = motionId,
            BaseRevision = revision
        };
        if (!Fields.TryApplyTo(request))
            return false;

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
        Conflict = await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
    }
}
