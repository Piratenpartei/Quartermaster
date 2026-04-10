using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingAgendaEdit {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

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
            Meeting = await Http.GetFromJsonAsync<MeetingDetailDTO>($"/api/meetings/{Id}");
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
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda", new AgendaItemCreateRequest {
                MeetingId = Id,
                ParentId = parentId,
                Title = "Neuer TOP",
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
        if (!await ConfirmDialog.ShowAsync("Diesen Tagesordnungspunkt wirklich löschen?"))
            return;
        try {
            await Http.DeleteAsync($"/api/meetings/{Id}/agenda/{itemId}");
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReorderAgendaItem((Guid ItemId, int Direction) args) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{args.ItemId}/reorder",
                new AgendaItemReorderRequest {
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
            await Http.PutAsJsonAsync($"/api/meetings/{Id}/agenda/{payload.ItemId}",
                new AgendaItemUpdateRequest {
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
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/move",
                new AgendaItemMoveRequest {
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
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/move",
                new AgendaItemMoveRequest {
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
            var resp = await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/import-motions",
                new { MeetingId = Id, ParentId = parentId });
            if (resp.IsSuccessStatusCode) {
                ToastService.Toast("Offene Anträge importiert.", "success");
                await LoadMeeting();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
