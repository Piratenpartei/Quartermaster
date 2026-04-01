using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Rendering;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class MarkdownEditor {
    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public int Rows { get; set; } = 8;

    [Parameter]
    public SanitizationProfile Profile { get; set; } = SanitizationProfile.Standard;

    private string RenderedHtml = "";
    private CancellationTokenSource? _debounce;

    private async Task OnInput(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            RenderedHtml = MarkdownService.ToHtml(Value, Profile);
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    protected override void OnParametersSet() {
        if (!string.IsNullOrWhiteSpace(Value))
            RenderedHtml = MarkdownService.ToHtml(Value, Profile);
    }
}
