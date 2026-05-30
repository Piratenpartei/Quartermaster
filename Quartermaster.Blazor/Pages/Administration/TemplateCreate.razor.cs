using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Templates;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class TemplateCreate {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }

    private string DisplayName { get; set; } = "";
    private bool SystemWide { get; set; }
    private string ChapterIdRaw { get; set; } = "";
    private bool Saving;

    private bool CanCreateSystemWide
        => AuthService.HasGlobalPermission(PermissionIdentifier.EditTemplates);

    private bool CanCreate
        => !string.IsNullOrWhiteSpace(DisplayName)
            && (SystemWide || Guid.TryParse(ChapterIdRaw, out _));

    private async Task Create() {
        if (!CanCreate)
            return;

        Saving = true;
        try {
            var request = new TemplateCreateRequest {
                DisplayName = DisplayName.Trim(),
                ChapterId = SystemWide ? null : Guid.Parse(ChapterIdRaw),
                Subject = "",
                Body = ""
            };
            var response = await Http.PostAsJsonAsync("/api/templates", request);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<TemplateListItemDTO>();
            if (created != null) {
                Navigation.NavigateTo($"/Administration/Templates/{created.Id}");
            } else {
                Navigation.NavigateTo("/Administration/Templates");
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Saving = false;
            StateHasChanged();
        }
    }
}
