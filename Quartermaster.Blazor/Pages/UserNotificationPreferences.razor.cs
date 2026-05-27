using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Notifications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class UserNotificationPreferences {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    private NotificationPreferencesDTO? Data;
    private Dictionary<(string TriggerId, string ChannelId), bool> _state = new();
    private bool Loading = true;
    private bool Saving;

    protected override async Task OnInitializedAsync() {
        await Load();
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
}
