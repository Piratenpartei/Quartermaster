using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Rendering;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class CodeMirrorEditorWithPreview : IDisposable {
    private CodeMirrorEditor? _editor;

    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public SanitizationProfile Profile { get; set; } = SanitizationProfile.Standard;

    [Parameter]
    public Guid? AgendaItemId { get; set; }

    [Parameter]
    public MeetingHubClient? HubClient { get; set; }

    [Parameter]
    public Guid? CurrentUserId { get; set; }

    [Parameter]
    public string? CurrentUserName { get; set; }

    private string RenderedHtml = "";
    private CancellationTokenSource? _debounce;

    private async Task OnInput(string value) {
        Value = value;
        await ValueChanged.InvokeAsync(Value);
        Form?.MarkDirty();

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

    protected override void OnAfterRender(bool firstRender) {
        if (firstRender && _editor != null) {
            _editor.PresenceChanged += OnPresenceChanged;
            _editor.StateChanged += OnEditorStateChanged;
        }
    }

    private void OnPresenceChanged() {
        InvokeAsync(StateHasChanged);
    }

    private void OnEditorStateChanged() {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() {
        if (_editor != null) {
            _editor.PresenceChanged -= OnPresenceChanged;
            _editor.StateChanged -= OnEditorStateChanged;
        }
    }

    private static string Initials(string name) {
        if (string.IsNullOrWhiteSpace(name))
            return "?";
        var parts = name.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpperInvariant();
    }
}
