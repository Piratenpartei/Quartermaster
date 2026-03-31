using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class MarkdownEditor {
    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public int Rows { get; set; } = 8;

    private string RenderedHtml = "";
    private CancellationTokenSource? _debounce;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private async Task OnInput(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            RenderedHtml = Markdown.ToHtml(Value, Pipeline);
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    protected override void OnParametersSet() {
        if (!string.IsNullOrWhiteSpace(Value))
            RenderedHtml = Markdown.ToHtml(Value, Pipeline);
    }
}
