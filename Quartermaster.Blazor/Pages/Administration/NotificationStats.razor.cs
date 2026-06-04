using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Notifications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class NotificationStats {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private DateTime? From;
    private DateTime? To;
    private string ChannelId = "";
    private string? TriggerId;
    private string? TemplateIdentifier;
    private string? SourceEntityType;
    private NotificationStatsGroupByDTO GroupBy = NotificationStatsGroupByDTO.None;

    private NotificationStatsCatalogDTO? Catalog;
    private NotificationStatsDTO? Stats;
    private bool Loading;

    private string FromIso => From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    private string ToIso => To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    protected override async Task OnInitializedAsync() {
        From = DateTime.UtcNow.Date.AddDays(-30);
        To = DateTime.UtcNow.Date.AddDays(1);
        await LoadCatalog();
        await LoadStats();
    }

    private async Task LoadCatalog() {
        try {
            Catalog = await Http.GetFromJsonAsync<NotificationStatsCatalogDTO>("/api/notifications/stats/catalog");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Catalog = new NotificationStatsCatalogDTO();
        }
    }

    private void OnFromChanged(ChangeEventArgs e) {
        From = ParseDate(e.Value?.ToString());
    }

    private void OnToChanged(ChangeEventArgs e) {
        To = ParseDate(e.Value?.ToString());
    }

    private void OnChannelSelected(ChangeEventArgs e) {
        ChannelId = e.Value?.ToString() ?? "";
    }

    private void OnTriggerChanged(string? value) {
        TriggerId = value;
    }

    private void OnTemplateChanged(string? value) {
        TemplateIdentifier = value;
    }

    private void OnSourceChanged(string? value) {
        SourceEntityType = value;
    }

    private static DateTime? ParseDate(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) {
            return parsed;
        }
        return null;
    }

    private Task ApplyFilters() => LoadStats();

    private async Task ResetFilters() {
        From = DateTime.UtcNow.Date.AddDays(-30);
        To = DateTime.UtcNow.Date.AddDays(1);
        ChannelId = "";
        TriggerId = null;
        TemplateIdentifier = null;
        SourceEntityType = null;
        GroupBy = NotificationStatsGroupByDTO.None;
        await LoadStats();
    }

    private async Task LoadStats() {
        Loading = true;
        StateHasChanged();
        try {
            var url = BuildUrl();
            Stats = await Http.GetFromJsonAsync<NotificationStatsDTO>(url);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private string BuildUrl() {
        var parts = new List<string>();
        if (From.HasValue) {
            parts.Add($"From={Uri.EscapeDataString(From.Value.ToString("o", CultureInfo.InvariantCulture))}");
        }
        if (To.HasValue) {
            parts.Add($"To={Uri.EscapeDataString(To.Value.ToString("o", CultureInfo.InvariantCulture))}");
        }
        if (!string.IsNullOrEmpty(ChannelId)) {
            parts.Add($"ChannelId={Uri.EscapeDataString(ChannelId)}");
        }
        if (!string.IsNullOrEmpty(TriggerId)) {
            parts.Add($"TriggerId={Uri.EscapeDataString(TriggerId)}");
        }
        if (!string.IsNullOrEmpty(TemplateIdentifier)) {
            parts.Add($"TemplateIdentifier={Uri.EscapeDataString(TemplateIdentifier)}");
        }
        if (!string.IsNullOrEmpty(SourceEntityType)) {
            parts.Add($"SourceEntityType={Uri.EscapeDataString(SourceEntityType)}");
        }
        parts.Add($"GroupBy={GroupBy}");
        return "/api/notifications/stats?" + string.Join("&", parts);
    }

    private string FormatReadRate(int received, int requested) {
        if (requested == 0) {
            return "-";
        }
        var ratio = (double)received / requested;
        return ratio.ToString("P1", CultureInfo.CurrentCulture);
    }

    private string FormatDuration(double seconds) {
        if (seconds <= 0) {
            return "-";
        }
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1) {
            return $"{(int)span.TotalDays}d {span.Hours}h";
        }
        if (span.TotalHours >= 1) {
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }
        if (span.TotalMinutes >= 1) {
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        }
        return $"{(int)span.TotalSeconds}s";
    }

    private string DisplayKey(string rawKey) {
        if (string.IsNullOrEmpty(rawKey)) {
            return I18n[I18nKey.Ui.NotificationStats.EmptyKey];
        }
        var lookup = GroupBy switch {
            NotificationStatsGroupByDTO.Trigger => Catalog?.Triggers,
            NotificationStatsGroupByDTO.Template => Catalog?.Templates,
            NotificationStatsGroupByDTO.SourceEntityType => Catalog?.SourceEntityTypes,
            NotificationStatsGroupByDTO.Channel => Catalog?.Channels,
            _ => null
        };
        if (lookup == null) {
            return rawKey;
        }
        foreach (var item in lookup) {
            if (item.Value == rawKey) {
                return item.DisplayName;
            }
        }
        return rawKey;
    }
}
