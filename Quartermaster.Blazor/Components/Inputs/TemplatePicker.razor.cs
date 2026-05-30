using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Templates;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class TemplatePicker {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid? Value { get; set; }

    [Parameter]
    public EventCallback<Guid?> ValueChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string SizeClass { get; set; } = "";

    private List<TemplateListItemDTO>? Templates;
    private string SearchText = "";
    private bool ShowDropdown;

    private List<TemplateListItemDTO> FilteredTemplates {
        get {
            if (Templates == null)
                return new();
            if (string.IsNullOrWhiteSpace(SearchText))
                return Templates;
            return Templates.Where(t =>
                t.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (t.Identifier != null && t.Identifier.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }

    protected override async Task OnInitializedAsync() {
        try {
            Templates = await Http.GetFromJsonAsync<List<TemplateListItemDTO>>("/api/templates");
        } catch (HttpRequestException) {
            Templates = new();
        }

        if (Value != null && Templates != null) {
            var selected = Templates.FirstOrDefault(t => t.Id == Value);
            if (selected != null)
                SearchText = selected.DisplayName;
        }
    }

    private void OnSearchInput(ChangeEventArgs e) {
        SearchText = e.Value?.ToString() ?? "";
        ShowDropdown = true;
        StateHasChanged();
    }

    private async Task SelectTemplate(TemplateListItemDTO template) {
        Value = template.Id;
        SearchText = template.DisplayName;
        ShowDropdown = false;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task ScheduleClose() {
        await Task.Delay(200);
        ShowDropdown = false;
        StateHasChanged();
    }
}
