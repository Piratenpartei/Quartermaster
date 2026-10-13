using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Rendering;
using Quartermaster.Blazor.Components.Forms;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class MarkdownEditorWithPreview {
    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public string? Id { get; set; }

    [Parameter]
    public int Rows { get; set; } = 8;

    [Parameter]
    public SanitizationProfile Profile { get; set; } = SanitizationProfile.Standard;

    [Parameter]
    public MarkdownFlavor Flavor { get; set; } = MarkdownFlavor.Full;

    private string RenderedHtml = "";
    private List<string> problemMessageKeys = [];
    private CancellationTokenSource? _debounce;

    private async Task OnInput(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);
        Form?.MarkDirty();

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            Render();
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    protected override void OnParametersSet() {
        if (string.IsNullOrWhiteSpace(Value)) {
            problemMessageKeys = [];
        } else {
            Render();
        }
    }

    private void Render() {
        RenderedHtml = MarkdownService.ToHtml(Value, Profile, Flavor);
        problemMessageKeys = Flavor == MarkdownFlavor.Motion
            ? MotionMarkdown.FindProblems(Value)
                .Select(MotionMarkdown.MessageKey)
                .ToList()
            : [];
    }
}
