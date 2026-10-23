using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Quartermaster.Blazor.Components.Inputs;

/// <summary>
/// A search box with a suggestion list underneath: up/down walks the list, Enter picks the highlighted entry, Escape closes it.
/// The caller owns the search text and the items; this component owns the open state and the highlight.
/// </summary>
public partial class SearchDropdown<TItem> {
    [Inject]
    public required IJSRuntime JS { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<TItem> Items { get; set; } = [];

    [Parameter, EditorRequired]
    public RenderFragment<TItem> ItemTemplate { get; set; } = default!;

    [Parameter]
    public EventCallback<TItem> OnSelect { get; set; }

    [Parameter]
    public string SearchText { get; set; } = "";

    [Parameter]
    public EventCallback<string> SearchTextChanged { get; set; }

    [Parameter]
    public EventCallback OnFocus { get; set; }

    [Parameter]
    public string Placeholder { get; set; } = "";

    /// <summary>Extra classes on the input, e.g. "form-control-sm".</summary>
    [Parameter]
    public string InputClass { get; set; } = "";

    /// <summary>Extra classes on the input group, e.g. "input-group-sm".</summary>
    [Parameter]
    public string GroupClass { get; set; } = "";

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Set when the dropdown sits inside a surrounding input group: it then stretches and drops its own rounded corners.</summary>
    [Parameter]
    public bool InInputGroup { get; set; }

    /// <summary>Open the list even without items, for callers whose header or footer says why it is empty.</summary>
    [Parameter]
    public bool ShowWithoutItems { get; set; }

    /// <summary>Input group addons before the input, e.g. a search icon.</summary>
    [Parameter]
    public RenderFragment? Prefix { get; set; }

    /// <summary>Input group addons after the input, e.g. a clear button.</summary>
    [Parameter]
    public RenderFragment? Suffix { get; set; }

    /// <summary>Rows above the items; they are clickable but not reached with the arrow keys.</summary>
    [Parameter]
    public RenderFragment? ListHeader { get; set; }

    /// <summary>Rows below the items; they are clickable but not reached with the arrow keys.</summary>
    [Parameter]
    public RenderFragment? ListFooter { get; set; }

    private bool Open;
    private int WantedHighlight = -1;
    private bool ScrollToHighlighted;
    private ElementReference _input;
    private ElementReference _list;

    /// <summary>The wanted highlight, kept while the items are still loading and capped to the items there are.</summary>
    private int Highlighted => Math.Min(WantedHighlight, Items.Count - 1);

    private string WrapperStyle => InInputGroup ? "flex: 1 1 auto; min-width: 0;" : "";

    private string InputStyle => InInputGroup ? "border-radius: 0;" : "";

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender)
            await JS.InvokeVoidAsync("qmDropdown.attach", _input);

        if (ScrollToHighlighted) {
            ScrollToHighlighted = false;
            await JS.InvokeVoidAsync("qmDropdown.scrollToHighlighted", _list);
        }
    }

    private bool IsOpen() {
        return Open && (Items.Count > 0 || ShowWithoutItems);
    }

    private async Task OnInput(ChangeEventArgs e) {
        Open = true;
        WantedHighlight = 0;
        await SearchTextChanged.InvokeAsync(e.Value?.ToString() ?? "");
    }

    private async Task OnInputFocus() {
        Open = true;
        WantedHighlight = -1;
        await OnFocus.InvokeAsync();
    }

    private async Task OnKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Escape") {
            Open = false;
            return;
        }
        if (!IsOpen()) {
            if (e.Key == "ArrowDown")
                Open = true;
            return;
        }

        switch (e.Key) {
            case "ArrowDown":
                WantedHighlight = Math.Min(Highlighted + 1, Items.Count - 1);
                ScrollToHighlighted = true;
                break;
            case "ArrowUp":
                WantedHighlight = Math.Max(Highlighted - 1, 0);
                ScrollToHighlighted = true;
                break;
            case "Enter":
                if (Highlighted >= 0)
                    await Select(Items[Highlighted]);
                break;
        }
    }

    private async Task Select(TItem item) {
        Open = false;
        WantedHighlight = -1;
        await OnSelect.InvokeAsync(item);
    }

    private async Task ScheduleClose() {
        await Task.Delay(200);
        Open = false;
        StateHasChanged();
    }
}
