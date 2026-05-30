using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Events;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventCreateFromTemplate {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid TemplateId { get; set; }

    private EventTemplateDetailDTO? Template;
    private bool Loading = true;
    private string ChapterId { get; set; } = "";
    private DateTime? EventDate { get; set; }
    private List<TemplateVariable> Variables = new();
    private bool Submitting;

    private string ResolvedPublicName {
        get {
            if (Template == null)
                return "";
            var result = Template.PublicNameTemplate;
            // Replace built-in date variables
            var dateStr = EventDate?.ToString("dd.MM.yyyy") ?? "";
            result = result.Replace("{{date}}", dateStr).Replace("{{datum}}", dateStr);
            // Replace custom variables
            foreach (var v in Variables) {
                result = result.Replace($"{{{{{v.Name}}}}}", v.Value);
            }
            return result;
        }
    }

    protected override async Task OnInitializedAsync() {
        try {
            Template = await Http.GetFromJsonAsync<EventTemplateDetailDTO>($"/api/eventtemplates/{TemplateId}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        if (Template != null) {
            Variables = Template.Variables.Select(v => new TemplateVariable {
                Name = v.Name,
                Label = v.Label,
                Type = v.Type,
                Value = ""
            }).ToList();
        }

        Loading = false;
    }

    private async Task Submit() {
        if (!Guid.TryParse(ChapterId, out var chapterId))
            return;

        Submitting = true;
        StateHasChanged();

        try {
            var variableValues = Variables.ToDictionary(v => v.Name, v => v.Value);

            var response = await Http.PostAsJsonAsync("/api/events/from-template", new EventFromTemplateRequest {
                TemplateId = TemplateId,
                ChapterId = chapterId,
                EventDate = EventDate.HasValue ? DateOnly.FromDateTime(EventDate.Value) : null,
                VariableValues = variableValues
            });

            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<EventDetailDTO>();
                if (result != null) {
                    ToastService.ToastKey(I18nKey.Ui.Toast.EventCreated);
                    Navigation.NavigateTo($"/Administration/Events/{result.Id}");
                }
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Submitting = false;
        StateHasChanged();
    }

    public class TemplateVariable {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "Text";
        public string Value { get; set; } = "";
    }
}
