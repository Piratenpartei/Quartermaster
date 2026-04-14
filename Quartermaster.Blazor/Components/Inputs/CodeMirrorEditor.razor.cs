using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Inputs;

/// <summary>
/// Blazor wrapper around the CodeMirror 5 editor (vendored, no build step).
/// Supports two modes:
///
/// <list type="bullet">
/// <item><b>Plain mode</b> (default): two-way binding via <see cref="Value"/>
/// + <see cref="ValueChanged"/>. Autosave/persistence is the parent's
/// responsibility.</item>
/// <item><b>Collab mode</b>: enabled by setting <see cref="AgendaItemId"/>
/// and passing a <see cref="MeetingHubClient"/>. The component loads the
/// initial Yjs snapshot from the hub, wires the CodemirrorBinding, and
/// forwards all edits via SignalR. A snapshot save timer runs on the
/// interval returned by the hub.</item>
/// </list>
///
/// The editor JS is loaded as a regular script tag in index.html which
/// exposes <c>window.cmEditor</c>; this component calls into it via
/// <see cref="IJSRuntime"/>.
/// </summary>
public partial class CodeMirrorEditor : IAsyncDisposable {
    [Inject]
    public required IJSRuntime JS { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public string CssClass { get; set; } = "";

    // --- Collab-mode parameters ---

    /// <summary>When set together with <see cref="HubClient"/>, switches to collaborative editing.</summary>
    [Parameter]
    public Guid? AgendaItemId { get; set; }

    [Parameter]
    public MeetingHubClient? HubClient { get; set; }

    [Parameter]
    public Guid? CurrentUserId { get; set; }

    [Parameter]
    public string? CurrentUserName { get; set; }

    private bool CollabMode => AgendaItemId.HasValue && HubClient != null;

    private ElementReference _hostElement;
    private DotNetObjectReference<CodeMirrorEditor>? _selfRef;
    private string? _handle;
    private string _lastKnownValue = "";
    private bool _readOnlyApplied;
    private bool _collabInitialized;
    private Timer? _snapshotTimer;
    private Guid _subscribedAgendaItemId;

    /// <summary>Current set of users connected to the document (for presence pills UI).</summary>
    public IReadOnlyList<PresenceEntry> CurrentPresence { get; private set; } = Array.Empty<PresenceEntry>();

    public event Action? PresenceChanged;

    public enum SaveState {
        /// <summary>No local changes since the last snapshot.</summary>
        Saved,
        /// <summary>Local changes exist but haven't been persisted yet.</summary>
        Dirty,
        /// <summary>A snapshot save is in flight.</summary>
        Saving,
        /// <summary>The last save attempt failed.</summary>
        Failed
    }

    /// <summary>Where we stand on persisting the document to the server.</summary>
    public SaveState CurrentSaveState { get; private set; } = SaveState.Saved;

    /// <summary>Wall-clock time of the last successful snapshot save (or null if never saved in this session).</summary>
    public DateTime? LastSavedAt { get; private set; }

    /// <summary>True if the document is in read-only mode (viewer permissions only, meeting frozen, or hub disconnected).</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>True while the hub connection is alive. Flips to false on disconnect, back to true on reconnect.</summary>
    public bool HubConnected { get; private set; } = true;

    public event Action? StateChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            _selfRef = DotNetObjectReference.Create(this);
            if (CollabMode) {
                await InitializeCollabAsync();
            } else {
                _handle = await JS.InvokeAsync<string>(
                    "cmEditor.createEditor", _hostElement, Value ?? "", ReadOnly, _selfRef);
                _lastKnownValue = Value ?? "";
                _readOnlyApplied = ReadOnly;
            }
        } else if (_handle != null && !CollabMode) {
            var incoming = Value ?? "";
            if (incoming != _lastKnownValue) {
                _lastKnownValue = incoming;
                await JS.InvokeVoidAsync("cmEditor.setText", _handle, incoming);
            }
            if (ReadOnly != _readOnlyApplied) {
                _readOnlyApplied = ReadOnly;
                await JS.InvokeVoidAsync("cmEditor.setReadOnly", _handle, ReadOnly);
            }
        }
    }

    private async Task InitializeCollabAsync() {
        if (_collabInitialized || HubClient == null || AgendaItemId == null)
            return;

        var snapshot = await HubClient.LoadDocumentAsync(AgendaItemId.Value);

        IsReadOnly = !snapshot.CanEdit;

        _handle = await JS.InvokeAsync<string>(
            "cmEditor.createCollabEditor",
            _hostElement,
            snapshot.PlainText,
            snapshot.DocumentStateBase64,
            snapshot.CanEdit,
            _selfRef);

        // Seed the client-side known-authors cache from the persistent
        // server snapshot BEFORE setCollabUser, so any author runs get
        // their proper color even before the first awareness round-trip.
        if (snapshot.KnownAuthors != null && snapshot.KnownAuthors.Count > 0) {
            await JS.InvokeVoidAsync("cmEditor.applyKnownAuthors", _handle, snapshot.KnownAuthors);
        }

        if (CurrentUserId.HasValue && !string.IsNullOrEmpty(CurrentUserName)) {
            await JS.InvokeVoidAsync("cmEditor.setCollabUser", _handle,
                CurrentUserId.Value.ToString(), CurrentUserName, snapshot.AssignedColor);
        }

        // Subscribe to presence changes so the parent can render pills.
        await JS.InvokeVoidAsync("cmEditor.subscribePresence", _handle, _selfRef);

        _subscribedAgendaItemId = AgendaItemId.Value;
        HubClient.UpdateReceived += OnHubUpdate;
        HubClient.AwarenessReceived += OnHubAwareness;
        HubClient.ConnectionStateChanged += OnHubConnectionStateChanged;
        HubConnected = HubClient.IsConnected;

        var intervalMs = Math.Max(2, snapshot.SaveIntervalSeconds) * 1000;
        _snapshotTimer = new Timer(_ => _ = TriggerSnapshotAsync(), null, intervalMs, intervalMs);

        _collabInitialized = true;

        // Fire the initial ValueChanged so the preview pane gets the seeded text.
        if (!string.IsNullOrEmpty(snapshot.PlainText) && ValueChanged.HasDelegate) {
            _lastKnownValue = snapshot.PlainText;
            await ValueChanged.InvokeAsync(snapshot.PlainText);
        }
    }

    private void OnHubUpdate(Guid agendaItemId, string updateBase64) {
        if (agendaItemId != _subscribedAgendaItemId || _handle == null)
            return;
        _ = JS.InvokeVoidAsync("cmEditor.applyRemoteUpdate", _handle, updateBase64).AsTask();
    }

    private void OnHubAwareness(Guid agendaItemId, string awarenessBase64) {
        if (agendaItemId != _subscribedAgendaItemId || _handle == null)
            return;
        _ = JS.InvokeVoidAsync("cmEditor.applyRemoteAwareness", _handle, awarenessBase64).AsTask();
    }

    private void OnHubConnectionStateChanged(bool connected) {
        HubConnected = connected;
        StateChanged?.Invoke();
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnJsValueChanged(string newValue) {
        _lastKnownValue = newValue;
        Value = newValue;
        if (ValueChanged.HasDelegate)
            await ValueChanged.InvokeAsync(newValue);
    }

    [JSInvokable]
    public async Task OnJsCollabUpdate(string updateBase64) {
        if (HubClient == null || AgendaItemId == null)
            return;
        // Read-only editors (anonymous viewers, ViewMeetings-only users,
        // frozen meetings) never try to push state. They still receive
        // remote updates via the hub relay; this hook is only for their
        // own local emissions, which shouldn't happen in read-only mode —
        // but our awareness bootstrap path can still fire it. No-op here
        // to keep the save indicator in its neutral "Saved" state.
        if (IsReadOnly)
            return;

        SetSaveState(SaveState.Dirty);

        try {
            await HubClient.SendUpdateAsync(AgendaItemId.Value, updateBase64);
        } catch {
            // Hub temporarily disconnected — update will be re-sent when
            // reconnection is re-established via full snapshot load.
        }
        // Also feed the preview pane with the latest plain text.
        if (_handle != null && ValueChanged.HasDelegate) {
            var snap = await JS.InvokeAsync<CollabSnapshotResult?>("cmEditor.getCollabSnapshot", _handle);
            if (snap != null && snap.PlainText != _lastKnownValue) {
                _lastKnownValue = snap.PlainText;
                Value = snap.PlainText;
                await ValueChanged.InvokeAsync(snap.PlainText);
            }
        }
    }

    [JSInvokable]
    public async Task OnJsAwarenessUpdate(string awarenessBase64) {
        if (HubClient == null || AgendaItemId == null)
            return;
        try {
            await HubClient.SendAwarenessAsync(AgendaItemId.Value, awarenessBase64);
        } catch {
            // Ignore — awareness is ephemeral.
        }
    }

    [JSInvokable]
    public Task OnJsPresenceChanged(List<PresenceEntry> entries) {
        CurrentPresence = entries ?? new List<PresenceEntry>();
        PresenceChanged?.Invoke();
        InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    private async Task TriggerSnapshotAsync() {
        if (_handle == null || HubClient == null || AgendaItemId == null)
            return;
        // Read-only editors never save; the snapshot timer is effectively
        // idle for them.
        if (IsReadOnly)
            return;
        // Nothing to save — skip the round-trip.
        if (CurrentSaveState == SaveState.Saved)
            return;
        SetSaveState(SaveState.Saving);
        try {
            var snap = await JS.InvokeAsync<CollabSnapshotResult?>("cmEditor.getCollabSnapshot", _handle);
            if (snap == null) {
                SetSaveState(SaveState.Dirty);
                return;
            }
            var knownAuthors = await JS.InvokeAsync<Dictionary<string, CollabAuthorInfo>>(
                "cmEditor.getKnownAuthors", _handle);
            await HubClient.SaveSnapshotAsync(new CollabSnapshotRequest {
                AgendaItemId = AgendaItemId.Value,
                DocumentStateBase64 = snap.DocumentStateBase64,
                PlainText = snap.PlainText,
                KnownAuthors = knownAuthors ?? new()
            });
            LastSavedAt = DateTime.Now;
            SetSaveState(SaveState.Saved);
        } catch {
            SetSaveState(SaveState.Failed);
        }
    }

    private void SetSaveState(SaveState newState) {
        if (CurrentSaveState == newState)
            return;
        CurrentSaveState = newState;
        StateChanged?.Invoke();
        InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync() {
        _snapshotTimer?.Dispose();
        if (_collabInitialized && HubClient != null) {
            HubClient.UpdateReceived -= OnHubUpdate;
            HubClient.AwarenessReceived -= OnHubAwareness;
            HubClient.ConnectionStateChanged -= OnHubConnectionStateChanged;
            // Flush one final snapshot on teardown.
            try {
                await TriggerSnapshotAsync();
            } catch { }
        }
        try {
            if (_handle != null)
                await JS.InvokeVoidAsync("cmEditor.dispose", _handle);
        } catch {
            // JS runtime may already be torn down.
        }
        _selfRef?.Dispose();
    }

    private class CollabSnapshotResult {
        public string DocumentStateBase64 { get; set; } = "";
        public string PlainText { get; set; } = "";
    }

    /// <summary>One user currently connected to the document.</summary>
    public class PresenceEntry {
        public long ClientId { get; set; }
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#1e88e5";
    }
}
