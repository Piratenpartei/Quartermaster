using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Notifications;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class StringFilterCombo {
    private const int MaxShown = 20;

    [Parameter]
    public required List<NotificationStatsFilterOptionDTO> Items { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public string AllOptionLabel { get; set; } = "";

    private string SearchText = "";
    private string? _lastValue;

    private List<NotificationStatsFilterOptionDTO> FilteredItems {
        get {
            if (string.IsNullOrWhiteSpace(SearchText)) {
                return Items;
            }
            return Items
                .Where(i =>
                    i.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || i.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private List<NotificationStatsFilterOptionDTO> ShownItems {
        get {
            return FilteredItems.Take(MaxShown).ToList();
        }
    }

    protected override void OnParametersSet() {
        if (Value != _lastValue) {
            _lastValue = Value;
            if (string.IsNullOrEmpty(Value)) {
                SearchText = "";
            } else {
                var match = Items.FirstOrDefault(i => i.Value == Value);
                SearchText = match?.DisplayName ?? Value;
            }
        }
    }

    private void OnSearchInput(string text) {
        SearchText = text;
    }

    private async Task SelectItem(NotificationStatsFilterOptionDTO item) {
        SearchText = item.DisplayName;
        _lastValue = item.Value;
        await ValueChanged.InvokeAsync(item.Value);
    }

    private async Task ClearSelection() {
        SearchText = "";
        _lastValue = null;
        await ValueChanged.InvokeAsync(null);
    }
}
