using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Api;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingAgendaEdit {
    [Inject]
    public required MeetingsApi MeetingsApi { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private MeetingDetailDTO? Meeting;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        await LoadMeeting();
    }

    private async Task LoadMeeting() {
        Loading = true;
        try {
            Meeting = await MeetingsApi.GetAsync(Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private Task AddAgendaItemChild(Guid parentId) => AddAgendaItem(parentId);

    private async Task AddAgendaItem(Guid? parentId) {
        if (Meeting == null)
            return;

        // Auto-parent under nearest preceding Section if no explicit parent given
        if (parentId == null)
            parentId = FindNearestPrecedingSection();

        try {
            await MeetingsApi.AddAgendaItemAsync(new AgendaItemCreateRequest {
                MeetingId = Id,
                ParentId = parentId,
                Title = I18n[I18nKey.Ui.MeetingAgendaEdit.NewItemDefaultTitle],
                ItemType = AgendaItemType.Discussion
            });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private Guid? FindNearestPrecedingSection() {
        if (Meeting == null)
            return null;

        var rootItems = Meeting.AgendaItems
            .Where(a => a.ParentId == null)
            .OrderBy(a => a.SortOrder)
            .ToList();

        // Find the last Section-type root item
        for (var i = rootItems.Count - 1; i >= 0; i--) {
            if (rootItems[i].ItemType == AgendaItemType.Section)
                return rootItems[i].Id;
        }
        return null;
    }

    private async Task DeleteAgendaItem(Guid itemId) {
        if (!await ConfirmDialog.ShowAsync(ToastService.Translate(I18nKey.Ui.Confirm.AgendaItemDelete)))
            return;
        try {
            await MeetingsApi.DeleteAgendaItemAsync(Id, itemId);
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReorderAgendaItem((Guid ItemId, int Direction) args) {
        try {
            await MeetingsApi.ReorderAgendaItemAsync(new AgendaItemReorderRequest {
                MeetingId = Id,
                ItemId = args.ItemId,
                Direction = args.Direction
            });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task UpdateAgendaItem(AgendaItemUpdatePayload payload) {
        if (Meeting == null)
            return;
        var existing = Meeting.AgendaItems.FirstOrDefault(a => a.Id == payload.ItemId);
        try {
            await MeetingsApi.UpdateAgendaItemAsync(new AgendaItemUpdateRequest {
                MeetingId = Id,
                ItemId = payload.ItemId,
                Title = payload.Title,
                ItemType = payload.ItemType,
                MotionId = payload.MotionId,
                Notes = existing?.Notes,
                Resolution = existing?.Resolution
            });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task IndentAgendaItem(Guid itemId) {
        if (Meeting == null)
            return;
        var item = Meeting.AgendaItems.FirstOrDefault(a => a.Id == itemId);
        if (item == null)
            return;

        // Find the preceding sibling at the same level
        var siblings = Meeting.AgendaItems
            .Where(a => a.ParentId == item.ParentId)
            .OrderBy(a => a.SortOrder)
            .ToList();
        var idx = siblings.FindIndex(a => a.Id == itemId);
        if (idx <= 0)
            return;

        var newParentId = siblings[idx - 1].Id;
        try {
            await MeetingsApi.MoveAgendaItemAsync(new AgendaItemMoveRequest {
                MeetingId = Id,
                ItemId = itemId,
                NewParentId = newParentId
            });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task OutdentAgendaItem(Guid itemId) {
        if (Meeting == null)
            return;
        var item = Meeting.AgendaItems.FirstOrDefault(a => a.Id == itemId);
        if (item?.ParentId == null)
            return;

        // Move to the parent's parent
        var parent = Meeting.AgendaItems.FirstOrDefault(a => a.Id == item.ParentId);
        var newParentId = parent?.ParentId;

        try {
            await MeetingsApi.MoveAgendaItemAsync(new AgendaItemMoveRequest {
                MeetingId = Id,
                ItemId = itemId,
                NewParentId = newParentId
            });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ImportMotions(Guid parentId) {
        try {
            var resp = await MeetingsApi.ImportMotionsAsync(Id, parentId);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.MotionsImported);
                await LoadMeeting();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
