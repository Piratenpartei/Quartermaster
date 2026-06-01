using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class AdminDivisionChipsInput {
    [Parameter]
    public List<Guid> Value { get; set; } = [];

    [Parameter]
    public EventCallback<List<Guid>> ValueChanged { get; set; }

    /// <summary>Display names parallel to <see cref="Value"/> for initial render — needed because no by-id endpoint exists, so the parent must supply the names it already knows.</summary>
    [Parameter]
    public List<string> InitialNames { get; set; } = [];

    private List<DivisionEntry> Selected = [];
    private AdminDivisionPicker? _picker;

    protected override void OnParametersSet() {
        if (Selected.Count == 0 && Value.Count > 0) {
            Selected = Value.Select((id, idx) => new DivisionEntry {
                Id = id,
                Name = idx < InitialNames.Count ? InitialNames[idx] : id.ToString()
            }).ToList();
        }
    }

    private async Task OnPicked(AdministrativeDivisionDTO div) {
        if (Selected.Any(e => e.Id == div.Id))
            return;
        var label = string.IsNullOrEmpty(div.AdminCode) ? div.Name : $"{div.Name} ({div.AdminCode})";
        Selected.Add(new DivisionEntry { Id = div.Id, Name = label });
        await ValueChanged.InvokeAsync(Selected.Select(e => e.Id).ToList());
    }

    private async Task Remove(Guid id) {
        Selected = Selected.Where(e => e.Id != id).ToList();
        await ValueChanged.InvokeAsync(Selected.Select(e => e.Id).ToList());
    }

    private class DivisionEntry {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }
}
