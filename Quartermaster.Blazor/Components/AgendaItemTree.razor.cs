using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Components;

public enum AgendaTreeMode {
    ReadOnly,
    Editor,
    MinuteTaking
}

public class AgendaItemUpdatePayload {
    public Guid ItemId { get; set; }
    public string Title { get; set; } = "";
    public AgendaItemType ItemType { get; set; }
    public Guid? MotionId { get; set; }
}

public class AgendaTreeEntry {
    public AgendaItemDTO Item { get; set; } = default!;
    public int Depth { get; set; }
    public string Number { get; set; } = "";
}

public partial class AgendaItemTree {
    [Parameter]
    public List<AgendaItemDTO> Items { get; set; } = new();

    [Parameter]
    public AgendaTreeMode Mode { get; set; } = AgendaTreeMode.ReadOnly;

    [Parameter]
    public Guid? ActiveItemId { get; set; }

    [Parameter]
    public string ChapterId { get; set; } = "";

    [Parameter]
    public EventCallback<Guid> OnAddChild { get; set; }

    [Parameter]
    public EventCallback<Guid> OnDelete { get; set; }

    [Parameter]
    public EventCallback<(Guid ItemId, int Direction)> OnReorder { get; set; }

    [Parameter]
    public EventCallback<AgendaItemUpdatePayload> OnUpdate { get; set; }

    [Parameter]
    public EventCallback<Guid> OnSelectActive { get; set; }

    private Guid? EditingItemId;
    private string EditingTitle = "";
    private int EditingItemType;
    private string EditingMotionId = "";

    private List<AgendaTreeEntry> FlatItems = new();

    protected override void OnParametersSet() {
        FlatItems = BuildFlatList();
    }

    private List<AgendaTreeEntry> BuildFlatList() {
        var result = new List<AgendaTreeEntry>();
        var byParent = Items
            .GroupBy(i => i.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(x => x.SortOrder).ToList());
        AppendLevel(result, byParent, null, 0, "");
        return result;
    }

    private void AppendLevel(
        List<AgendaTreeEntry> result,
        Dictionary<Guid, List<AgendaItemDTO>> byParent,
        Guid? parentId,
        int depth,
        string prefix) {
        var key = parentId ?? Guid.Empty;
        if (!byParent.TryGetValue(key, out var children))
            return;
        var idx = 1;
        foreach (var child in children) {
            var number = string.IsNullOrEmpty(prefix) ? idx.ToString() : $"{prefix}.{idx}";
            result.Add(new AgendaTreeEntry { Item = child, Depth = depth, Number = number });
            AppendLevel(result, byParent, child.Id, depth + 1, number);
            idx++;
        }
    }

    private void StartEdit(AgendaItemDTO item) {
        EditingItemId = item.Id;
        EditingTitle = item.Title;
        EditingItemType = (int)item.ItemType;
        EditingMotionId = item.MotionId?.ToString() ?? "";
    }

    private void CancelEdit() {
        EditingItemId = null;
    }

    private void OnEditingMotionChanged(string value) {
        EditingMotionId = value;
    }

    private async Task SaveEdit(AgendaItemDTO item) {
        if (EditingItemId == null)
            return;
        Guid? motionId = null;
        if (Guid.TryParse(EditingMotionId, out var mid))
            motionId = mid;
        await OnUpdate.InvokeAsync(new AgendaItemUpdatePayload {
            ItemId = item.Id,
            Title = EditingTitle,
            ItemType = (AgendaItemType)EditingItemType,
            MotionId = motionId
        });
        EditingItemId = null;
    }

    private static string TypeLabel(AgendaItemType t) => t switch {
        AgendaItemType.Discussion => "Diskussion",
        AgendaItemType.Motion => "Antrag",
        AgendaItemType.Protocol => "Protokoll",
        AgendaItemType.Break => "Pause",
        AgendaItemType.Information => "Information",
        _ => t.ToString()
    };

    private static string TypeBadgeClass(AgendaItemType t) => t switch {
        AgendaItemType.Discussion => "border-secondary text-secondary-emphasis",
        AgendaItemType.Motion => "border-primary text-primary-emphasis",
        AgendaItemType.Protocol => "border-info text-info-emphasis",
        AgendaItemType.Break => "border-warning text-warning-emphasis",
        AgendaItemType.Information => "border-success text-success-emphasis",
        _ => "border-secondary"
    };
}
