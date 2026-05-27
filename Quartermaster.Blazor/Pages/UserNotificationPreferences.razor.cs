using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Notifications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class UserNotificationPreferences : IDisposable {
    private static readonly TimeSpan LinkPollInterval = TimeSpan.FromSeconds(5);

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    private NotificationPreferencesDTO? Data;
    private Dictionary<(string TriggerId, string ChannelId), bool> _state = new();
    private bool Loading = true;
    private bool Saving;

    private TelegramLinkStatusDTO? TelegramStatus;
    private TelegramLinkStartDTO? TelegramLinkStart;
    private bool TelegramBusy;
    private bool LinkCommandCopied;
    private Timer? _linkPollTimer;
    private string LinkCommand => TelegramLinkStart == null ? "" : $"/link {TelegramLinkStart.Token}";

    protected override async Task OnInitializedAsync() {
        await Task.WhenAll(Load(), LoadTelegramStatus());
    }

    private async Task Load() {
        Loading = true;
        try {
            Data = await Http.GetFromJsonAsync<NotificationPreferencesDTO>("/api/users/notification-preferences");
            _state = Data?.Cells.ToDictionary(c => (c.TriggerId, c.ChannelId), c => c.Enabled) ?? new();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private async Task LoadTelegramStatus() {
        try {
            TelegramStatus = await Http.GetFromJsonAsync<TelegramLinkStatusDTO>("/api/users/telegram-link");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        StateHasChanged();
    }

    private async Task StartLink() {
        TelegramBusy = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsync("/api/users/telegram-link", null);
            if (resp.IsSuccessStatusCode) {
                TelegramLinkStart = await resp.Content.ReadFromJsonAsync<TelegramLinkStartDTO>();
                StartLinkPolling();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            TelegramBusy = false;
            StateHasChanged();
        }
    }

    private void CancelLinkStart() {
        StopLinkPolling();
        TelegramLinkStart = null;
        LinkCommandCopied = false;
    }

    private async Task CopyLinkCommand() {
        if (TelegramLinkStart == null) {
            return;
        }
        try {
            var ok = await JS.InvokeAsync<bool>("CopyToClipboard", LinkCommand);
            if (ok) {
                LinkCommandCopied = true;
                StateHasChanged();
            }
        } catch (JSException ex) {
            ToastService.Error(ex);
        }
    }

    /// <summary>Triggered by the manual "Check Link" button and the background timer.</summary>
    private async Task CheckLinkStatus() {
        await LoadTelegramStatus();
        if (TelegramStatus?.Linked == true) {
            StopLinkPolling();
            TelegramLinkStart = null;
            LinkCommandCopied = false;
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            await Load();
            StateHasChanged();
        }
    }

    private void StartLinkPolling() {
        _linkPollTimer?.Dispose();
        _linkPollTimer = new Timer(_ => {
            _ = InvokeAsync(CheckLinkStatus);
        }, null, LinkPollInterval, LinkPollInterval);
    }

    private void StopLinkPolling() {
        _linkPollTimer?.Dispose();
        _linkPollTimer = null;
    }

    private async Task Unlink() {
        TelegramBusy = true;
        StateHasChanged();
        try {
            var resp = await Http.DeleteAsync("/api/users/telegram-link");
            if (resp.IsSuccessStatusCode) {
                TelegramStatus = new TelegramLinkStatusDTO { Linked = false };
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                await Load();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            TelegramBusy = false;
            StateHasChanged();
        }
    }

    private bool IsChecked((string TriggerId, string ChannelId) key)
        => _state.TryGetValue(key, out var v) && v;

    private void OnToggle((string TriggerId, string ChannelId) key, bool enabled) {
        _state[key] = enabled;
    }

    private async Task Save() {
        Saving = true;
        StateHasChanged();
        try {
            var req = new UpdateNotificationPreferencesRequest {
                Cells = _state.Select(kv => new NotificationPreferenceCellDTO {
                    TriggerId = kv.Key.TriggerId,
                    ChannelId = kv.Key.ChannelId,
                    Enabled = kv.Value
                }).ToList()
            };
            var resp = await Http.PutAsJsonAsync("/api/users/notification-preferences", req);
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Saving = false;
            StateHasChanged();
        }
    }

    public void Dispose() {
        StopLinkPolling();
    }
}
