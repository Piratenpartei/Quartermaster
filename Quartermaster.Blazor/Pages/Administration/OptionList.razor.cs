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

    private static string DataTypeLabel(int dt) => dt switch {
        0 => "Text",
        1 => "Zahl",
        2 => "Template",
        _ => "?"
    };

    private static string DataTypeBadge(int dt) => dt switch {
        0 => "border-info text-info-emphasis",
        1 => "border-primary text-primary-emphasis",
        2 => "border-warning text-warning-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
