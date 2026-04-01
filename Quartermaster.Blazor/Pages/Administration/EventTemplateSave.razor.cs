using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventTemplateSave {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private EventDetailDTO? Event;
    private bool Loading = true;
    private string TemplateName { get; set; } = "";
    private List<VariableDefinition> Variables = new();
    private bool Saving;

    protected override async Task OnInitializedAsync() {
        try {
            Event = await Http.GetFromJsonAsync<EventDetailDTO>($"/api/events/{EventId}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        if (Event != null) {
            TemplateName = Event.InternalName;
            DetectVariables();
        }

        Loading = false;
    }

    private void DetectVariables() {
        var allText = new List<string> { Event!.PublicName, Event.Description ?? "" };
        foreach (var item in Event.ChecklistItems) {
            allText.Add(item.Label);
            if (item.Configuration != null)
                allText.Add(item.Configuration);
        }

        // Built-in variables that map to event model fields — exclude from custom variables
        var builtInVariables = new HashSet<string> { "date", "datum" };

        var variableNames = new HashSet<string>();
        var regex = new Regex(@"\{\{(\w+)\}\}");
        foreach (var text in allText) {
            foreach (Match match in regex.Matches(text)) {
                var name = match.Groups[1].Value;
                if (!builtInVariables.Contains(name))
                    variableNames.Add(name);
            }
        }

        Variables = variableNames.Select(name => new VariableDefinition {
            Name = name,
            Label = name,
            Type = "Text"
        }).ToList();
    }

    private async Task Save() {
        if (string.IsNullOrWhiteSpace(TemplateName))
            return;

        Saving = true;
        StateHasChanged();

        try {
            var variablesJson = JsonSerializer.Serialize(Variables.Select(v => new {
                name = v.Name,
                label = v.Label,
                type = v.Type
            }));

            await Http.PostAsJsonAsync("/api/eventtemplates", new EventTemplateCreateRequest {
                EventId = EventId,
                Name = TemplateName,
                Variables = variablesJson
            });

            Navigation.NavigateTo($"/Administration/Events/{EventId}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Saving = false;
            StateHasChanged();
        }
    }

    public class VariableDefinition {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "Text";
    }
}
