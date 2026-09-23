using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Forms;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class PdfTemplateList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private List<PdfTemplateListItemDTO>? Items;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Items = await Http.GetFromJsonAsync<List<PdfTemplateListItemDTO>>("/api/pdf-templates") ?? new();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }
}
