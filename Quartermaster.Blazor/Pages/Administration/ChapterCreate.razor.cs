using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterCreate {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private string Name = "";
    private string ShortCode = "";
    private string ExternalCode = "";
    private string ParentChapterId = "";
    private string AdministrativeDivisionId = "";
    private bool Saving;

    [SupplyParameterFromQuery]
    public string? Parent { get; set; }

    protected override void OnInitialized() {
        if (!string.IsNullOrEmpty(Parent)) {
            ParentChapterId = Parent;
        }
    }

    private void OnParentChanged(string id) {
        ParentChapterId = id;
    }

    private void OnDivisionChanged(string id) {
        AdministrativeDivisionId = id;
    }

    private async Task Save() {
        Saving = true;
        StateHasChanged();
        try {
            var req = new ChapterCreateRequest {
                Name = Name.Trim(),
                ShortCode = string.IsNullOrWhiteSpace(ShortCode) ? null : ShortCode.Trim(),
                ExternalCode = string.IsNullOrWhiteSpace(ExternalCode) ? null : ExternalCode.Trim(),
                ParentChapterId = Guid.TryParse(ParentChapterId, out var parsed) ? parsed : null,
                AdministrativeDivisionId = Guid.TryParse(AdministrativeDivisionId, out var divParsed) ? divParsed : null
            };
            var resp = await Http.PostAsJsonAsync("/api/chapters", req);
            if (resp.IsSuccessStatusCode) {
                var created = await resp.Content.ReadFromJsonAsync<ChapterDTO>();
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                Navigation.NavigateTo($"/Administration/Chapters/{created!.Id}");
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
