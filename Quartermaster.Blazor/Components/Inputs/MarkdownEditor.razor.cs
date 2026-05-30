using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Rendering;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class MarkdownEditor {
    [Inject]
    public required IJSRuntime JS { get; set; }

    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public int Rows { get; set; } = 8;

    [Parameter]
    public SanitizationProfile Profile { get; set; } = SanitizationProfile.Standard;

    /// <summary>Optional pre-processor applied to the markdown source before it is rendered for the preview pane.</summary>
    [Parameter]
    public Func<string, Task<string>>? PreviewTransform { get; set; }

    private string RenderedHtml = "";
    private CancellationTokenSource? _debounce;
    private ElementReference _textarea;

    private async Task OnInput(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);
        Form?.MarkDirty();

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            await UpdatePreviewAsync();
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    protected override async Task OnParametersSetAsync() {
        if (!string.IsNullOrWhiteSpace(Value))
            await UpdatePreviewAsync();
    }

    private async Task UpdatePreviewAsync() {
        var source = PreviewTransform != null ? await PreviewTransform(Value) : Value;
        RenderedHtml = MarkdownService.ToHtml(source, Profile);
    }

    public ValueTask InsertAtCursorAsync(string text)
        => JS.InvokeVoidAsync("MarkdownEditorInsertAtCursor", _textarea, text);
}
