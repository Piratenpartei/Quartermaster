using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public partial class SettingsHub {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private static readonly SettingsGroup[] Groups = Enum.GetValues<SettingsGroup>();

    private Dictionary<string, OptionDefinitionDTO>? Options;

    protected override async Task OnInitializedAsync() {
        try {
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? [];
            Options = options.ToDictionary(o => o.Identifier);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Options = [];
        }
    }

    private static string GroupTitleKey(SettingsGroup group) {
        return group switch {
            SettingsGroup.Basics => I18nKey.Ui.Settings.GroupBasics,
            SettingsGroup.Login => I18nKey.Ui.Settings.GroupLogin,
            SettingsGroup.Communication => I18nKey.Ui.Settings.GroupCommunication,
            SettingsGroup.Integrations => I18nKey.Ui.Settings.GroupIntegrations,
            _ => I18nKey.Ui.Settings.GroupAssembly
        };
    }

    private static string StatusKey(SettingsStatus status) {
        return status switch {
            SettingsStatus.Configured => I18nKey.Ui.Settings.StatusConfigured,
            SettingsStatus.Partial => I18nKey.Ui.Settings.StatusPartial,
            SettingsStatus.On => I18nKey.Ui.Settings.StatusOn,
            SettingsStatus.Off => I18nKey.Ui.Settings.StatusOff,
            _ => I18nKey.Ui.Settings.StatusNotConfigured
        };
    }

    private static StatusTone ToneOf(SettingsStatus status) {
        return status switch {
            SettingsStatus.Configured => StatusTone.Success,
            SettingsStatus.On => StatusTone.Success,
            SettingsStatus.Partial => StatusTone.Warning,
            _ => StatusTone.Neutral
        };
    }
}
