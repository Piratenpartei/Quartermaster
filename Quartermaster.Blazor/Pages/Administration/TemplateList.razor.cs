using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Templates;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class TemplateList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private List<TemplateListItemDTO>? Templates;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        Loading = true;
        try {
            Templates = await Http.GetFromJsonAsync<List<TemplateListItemDTO>>("/api/templates");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private string TypeLabel(TemplateListItemDTO t) {
        if (t.IsSystem && t.ChapterId == null)
            return I18n[I18nKey.Ui.TemplateList.TypeSystem];
        if (!t.IsSystem && t.ChapterId == null)
            return I18n[I18nKey.Ui.TemplateList.TypeSystemWide];
        return I18n[I18nKey.Ui.TemplateList.TypeChapter];
    }
}
