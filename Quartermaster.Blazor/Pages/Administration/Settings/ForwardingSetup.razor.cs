using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

/// <summary>Master switches, thresholds, the federal settings and chapter overrides of the forwarding.</summary>
public partial class ForwardingSetup {
    private const string TrueValue = "true";

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private bool Loading = true;
    private bool Busy;
    private bool ApplicationsEnabled;
    private bool DuesEnabled;
    private decimal RegularMinimum = ForwardingOptionKeys.DefaultRegularMinimum;
    private decimal StateMinimum = ForwardingOptionKeys.DefaultStateDecisionMinimum;
    private ForwardingChapterSettingDTO? Federal;
    private List<ForwardingChapterSettingDTO> Chapters = [];
    private ForwardingChapterSettingDTO? NewChapter;
    private Guid? EditingChapterId;
    private string NewChapterId = "";
    private ConfirmDialog RemoveDialog = default!;

    protected override async Task OnInitializedAsync() {
        try {
            await ReloadAsync();
        } finally {
            Loading = false;
        }
    }

    private async Task ReloadAsync() {
        try {
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? [];
            var byId = options.ToDictionary(o => o.Identifier);
            ApplicationsEnabled = IsTrue(byId.GetValueOrDefault(ForwardingOptionKeys.MembershipApplicationEnabled)?.GlobalValue);
            DuesEnabled = IsTrue(byId.GetValueOrDefault(ForwardingOptionKeys.DueSelectionEnabled)?.GlobalValue);
            RegularMinimum = ParseAmount(byId.GetValueOrDefault(ForwardingOptionKeys.RegularMinimum)?.GlobalValue, ForwardingOptionKeys.DefaultRegularMinimum);
            StateMinimum = ParseAmount(byId.GetValueOrDefault(ForwardingOptionKeys.StateDecisionMinimum)?.GlobalValue, ForwardingOptionKeys.DefaultStateDecisionMinimum);

            var chapters = await Http.GetFromJsonAsync<List<ForwardingChapterSettingDTO>>("/api/forwarding/chapters") ?? [];
            Federal = chapters.FirstOrDefault(c => c.IsFederal);
            Chapters = chapters.Where(c => !c.IsFederal).ToList();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<Guid> ConfiguredChapterIds() {
        var ids = Chapters.Select(c => c.ChapterId).ToList();
        if (Federal != null)
            ids.Add(Federal.ChapterId);
        return ids;
    }

    private string ModeLabel(ForwardingMode? mode) {
        return mode switch {
            ForwardingMode.Quartermaster => I18n[I18nKey.Ui.ForwardingSetup.ModeQuartermaster],
            ForwardingMode.Redmine => I18n[I18nKey.Ui.ForwardingSetup.ModeRedmine],
            _ => I18n[I18nKey.Ui.ForwardingSetup.ModeUnset]
        };
    }

    private async Task StartNewAsync() {
        if (!Guid.TryParse(NewChapterId, out var chapterId))
            return;

        try {
            var all = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters") ?? [];
            var chapter = all.FirstOrDefault(c => c.Id == chapterId);
            if (chapter == null)
                return;

            var parent = chapter.ParentChapterId == null ? null : all.FirstOrDefault(c => c.Id == chapter.ParentChapterId);
            var connections = await Http.GetFromJsonAsync<List<RedmineConnectionDTO>>("/api/forwarding/redmine-connections") ?? [];
            NewChapter = new ForwardingChapterSettingDTO {
                ChapterId = chapter.Id,
                ChapterName = chapter.Name,
                IsStateChapter = parent != null && parent.ParentChapterId == null,
                HasConnection = HasConnectionUpTree(chapter, all, connections)
            };
            NewChapterId = "";
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private static bool HasConnectionUpTree(ChapterDTO chapter, List<ChapterDTO> all, List<RedmineConnectionDTO> connections) {
        var connected = connections.Select(c => c.ChapterId).ToHashSet();
        ChapterDTO? current = chapter;
        while (current != null) {
            if (connected.Contains(current.Id))
                return true;
            current = current.ParentChapterId == null ? null : all.FirstOrDefault(c => c.Id == current.ParentChapterId);
        }

        return false;
    }

    private async Task SavedAsync() {
        NewChapter = null;
        EditingChapterId = null;
        await ReloadAsync();
    }

    private async Task RemoveAsync(Guid chapterId) {
        if (!await RemoveDialog.ShowAsync(I18n[I18nKey.Ui.ForwardingSetup.RemoveConfirm]))
            return;

        await RunAsync(() => Http.DeleteAsync($"/api/forwarding/chapters/{chapterId}"));
    }

    private Task SetSwitchAsync(string key, bool value) {
        // The click is mirrored first: when the server refuses, the reload sets the value back and Blazor redraws the switch, which it would not do
        // for a value that never changed.
        if (key == ForwardingOptionKeys.MembershipApplicationEnabled) {
            ApplicationsEnabled = value;
        } else {
            DuesEnabled = value;
        }

        return RunAsync(() => Http.PostAsJsonAsync("/api/forwarding/switches", new ForwardingSwitchRequest { Key = key, Enabled = value }));
    }

    private async Task SaveThresholdsAsync() {
        if (StateMinimum <= 0 || StateMinimum >= RegularMinimum) {
            ToastService.ToastKey(I18nKey.Error.Forwarding.ThresholdsInvalid, "danger");
            return;
        }

        await RunAsync(async () => {
            var regular = await SaveOptionAsync(ForwardingOptionKeys.RegularMinimum, RegularMinimum);
            if (!regular.IsSuccessStatusCode)
                return regular;

            regular.Dispose();
            return await SaveOptionAsync(ForwardingOptionKeys.StateDecisionMinimum, StateMinimum);
        });
    }

    private Task<HttpResponseMessage> SaveOptionAsync(string key, decimal value) {
        return Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest { Identifier = key, Value = value.ToString(CultureInfo.InvariantCulture) });
    }

    private async Task RunAsync(Func<Task<HttpResponseMessage>> send) {
        Busy = true;
        try {
            using var response = await send();
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
            } else {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            }

            await ReloadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private static bool IsTrue(string? value) {
        return string.Equals(value, TrueValue, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ParseAmount(string? value, decimal fallback) {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0 ? amount : fallback;
    }
}
