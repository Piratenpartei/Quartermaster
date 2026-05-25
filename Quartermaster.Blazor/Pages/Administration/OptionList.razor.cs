using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OptionList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<OptionDefinitionDTO>? Options;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private static string DataTypeLabel(OptionDataType dt) => dt switch {
        OptionDataType.String => "Text",
        OptionDataType.Number => "Zahl",
        OptionDataType.Template => "Template",
        _ => "?"
    };

    private static string DataTypeBadge(OptionDataType dt) => dt switch {
        OptionDataType.String => "border-info text-info-emphasis",
        OptionDataType.Number => "border-primary text-primary-emphasis",
        OptionDataType.Template => "border-warning text-warning-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
