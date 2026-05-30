using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Api;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Meetings;

public partial class AgendaItemListEditor {
    [Inject]
    public required MeetingsApi MeetingsApi { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public required Guid MeetingId { get; set; }

    [Parameter]
    public required MeetingDetailDTO Meeting { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    private ConfirmDialog ConfirmDialog = default!;

    private Task AddAgendaItemChild(Guid parentId) => AddAgendaItem(parentId);

    private async Task AddAgendaItem(Guid? parentId) {
        if (parentId == null)
            parentId = FindNearestPrecedingSection();

        try {
            await MeetingsApi.AddAgendaItemAsync(new AgendaItemCreateRequest {
                MeetingId = MeetingId,
                ParentId = parentId,
                Title = "Neuer TOP",
                ItemType = AgendaItemType.Discussion
            });
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private Guid? FindNearestPrecedingSection() {
        var rootItems = Meeting.AgendaItems
            .Where(a => a.ParentId == null)
            .OrderBy(a => a.SortOrder)
            .ToList();

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
            await MeetingsApi.DeleteAgendaItemAsync(MeetingId, itemId);
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReorderAgendaItem((Guid ItemId, int Direction) args) {
        try {
            await MeetingsApi.ReorderAgendaItemAsync(new AgendaItemReorderRequest {
                MeetingId = MeetingId,
                ItemId = args.ItemId,
                Direction = args.Direction
            });
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task UpdateAgendaItem(AgendaItemUpdatePayload payload) {
        var existing = Meeting.AgendaItems.FirstOrDefault(a => a.Id == payload.ItemId);
        try {
            await MeetingsApi.UpdateAgendaItemAsync(new AgendaItemUpdateRequest {
                MeetingId = MeetingId,
                ItemId = payload.ItemId,
                Title = payload.Title,
                ItemType = payload.ItemType,
                MotionId = payload.MotionId,
                Notes = existing?.Notes,
                Resolution = existing?.Resolution
            });
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task IndentAgendaItem(Guid itemId) {
        var item = Meeting.AgendaItems.FirstOrDefault(a => a.Id == itemId);
        if (item == null)
            return;

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
                MeetingId = MeetingId,
                ItemId = itemId,
                NewParentId = newParentId
            });
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task OutdentAgendaItem(Guid itemId) {
        var item = Meeting.AgendaItems.FirstOrDefault(a => a.Id == itemId);
        if (item?.ParentId == null)
            return;

        var parent = Meeting.AgendaItems.FirstOrDefault(a => a.Id == item.ParentId);
        var newParentId = parent?.ParentId;

        try {
            await MeetingsApi.MoveAgendaItemAsync(new AgendaItemMoveRequest {
                MeetingId = MeetingId,
                ItemId = itemId,
                NewParentId = newParentId
            });
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ImportMotions(Guid parentId) {
        try {
            var resp = await MeetingsApi.ImportMotionsAsync(MeetingId, parentId);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.MotionsImported);
                await OnChanged.InvokeAsync();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
