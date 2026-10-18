using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Pages.Administration.Settings;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OptionList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private List<OptionDefinitionDTO>? Options;
    private bool Loading = true;
    private string Search = "";

    protected override async Task OnInitializedAsync() {
        try {
            Options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private IEnumerable<OptionDefinitionDTO> FilteredOptions() {
        if (Options == null)
            return [];
        if (string.IsNullOrWhiteSpace(Search))
            return Options;

        var term = Search.Trim();
        return Options.Where(o => o.Identifier.Contains(term, StringComparison.OrdinalIgnoreCase)
            || o.FriendlyName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private string DataTypeLabel(OptionDataType dt) => dt switch {
        OptionDataType.String => I18n[I18nKey.Ui.OptionList.DataTypeString],
        OptionDataType.Number => I18n[I18nKey.Ui.OptionList.DataTypeNumber],
        _ => I18n[I18nKey.Ui.OptionList.DataTypeUnknown]
    };

    private static string DataTypeBadge(OptionDataType dt) => dt switch {
        OptionDataType.String => "border-info text-info-emphasis",
        OptionDataType.Number => "border-primary text-primary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
