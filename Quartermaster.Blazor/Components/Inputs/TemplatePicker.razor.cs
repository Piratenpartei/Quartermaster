using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Options;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class TemplatePicker {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string SizeClass { get; set; } = "";

    private List<OptionDefinitionDTO>? Templates;
    private string SearchText = "";
    private bool ShowDropdown;

    private List<OptionDefinitionDTO> FilteredTemplates {
        get {
            if (Templates == null)
                return new();
            if (string.IsNullOrWhiteSpace(SearchText))
                return Templates;
            return Templates.Where(t =>
                t.FriendlyName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || t.Identifier.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    protected override async Task OnInitializedAsync() {
        Templates = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");

        if (!string.IsNullOrEmpty(Value) && Templates != null) {
            var selected = Templates.FirstOrDefault(t => t.Identifier == Value);
            if (selected != null)
                SearchText = selected.FriendlyName;
            else
                SearchText = Value;
        }
    }

    private void OnSearchInput(ChangeEventArgs e) {
        SearchText = e.Value?.ToString() ?? "";
        ShowDropdown = true;
        StateHasChanged();
    }

    private async Task SelectTemplate(OptionDefinitionDTO template) {
        Value = template.Identifier;
        SearchText = template.FriendlyName;
        ShowDropdown = false;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task ScheduleClose() {
        await Task.Delay(200);
        ShowDropdown = false;
        StateHasChanged();
    }
}
