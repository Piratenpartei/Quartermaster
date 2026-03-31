using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.Events;
using Quartermaster.Api.Rendering;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private EventDetailDTO? Event;
    private bool Loading = true;
    private bool Saving;
    private bool IsDirty;
    private bool EditingTitle;

    // Add item
    private string NewItemLabel { get; set; } = "";
    private int NewItemType { get; set; } = 0;

    // Inline edit
    private Guid? EditingItemId;
    private string EditingItemLabel { get; set; } = "";
    private bool EditingUseDescription;
    private string EditingTemplateIdentifier { get; set; } = "";
    private string EditingEmailTargetType { get; set; } = "Chapter";
    private string EditingEmailTargetId { get; set; } = "";
    private string EditingManualAddresses { get; set; } = "";
    private string EditingMotionChapterId { get; set; } = "";
    private string EditingMotionTitle { get; set; } = "";
    private string EditingMotionText { get; set; } = "";

    // Email preview
    private Guid? ExpandedPreviewItemId;
    private Dictionary<Guid, string?> PreviewCache = new();

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    protected override async Task OnInitializedAsync() {
        await LoadEvent();
    }

    private async Task LoadEvent() {
        Loading = true;
        try {
            Event = await Http.GetFromJsonAsync<EventDetailDTO>($"/api/events/{Id}");
        } catch (HttpRequestException) { }
        Loading = false;
        IsDirty = false;
        StateHasChanged();
    }

    private void MarkDirty() {
        IsDirty = true;
    }

    private void OnDescriptionChanged(string value) {
        if (Event != null) {
            Event.Description = value;
            IsDirty = true;
        }
    }

    private async Task SaveDetails() {
        if (Event == null)
            return;

        Saving = true;
        StateHasChanged();

        await Http.PutAsJsonAsync($"/api/events/{Id}", new EventUpdateRequest {
            Id = Id,
            InternalName = Event.InternalName,
            PublicName = Event.PublicName,
            Description = Event.Description,
            EventDate = Event.EventDate
        });

        Saving = false;
        IsDirty = false;
        StateHasChanged();
    }

    private async Task SaveTitleEdit() {
        EditingTitle = false;
        IsDirty = true;
        await SaveDetails();
    }

    private async Task SaveIfDirty() {
        if (IsDirty && Event != null) {
            await Http.PutAsJsonAsync($"/api/events/{Id}", new EventUpdateRequest {
                Id = Id,
                InternalName = Event.InternalName,
                PublicName = Event.PublicName,
                Description = Event.Description,
                EventDate = Event.EventDate
            });
            IsDirty = false;
        }
    }

    // Checklist actions
    private async Task CheckTextItem(Guid itemId) {
        await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/check",
            new { executeAction = false });
        await SaveIfDirty();
        await LoadEvent();
    }

    private async Task UncheckItem(Guid itemId) {
        await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/uncheck", new { });
        await SaveIfDirty();
        await LoadEvent();
    }

    private async Task CheckActionItem(Guid itemId, bool executeAction) {
        await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/check",
            new { executeAction });
        await SaveIfDirty();
        await LoadEvent();
    }

    // Add item
    private async Task AddItem(int itemType) {
        if (string.IsNullOrWhiteSpace(NewItemLabel))
            return;

        var nextSortOrder = Event?.ChecklistItems.Count ?? 0;

        await Http.PostAsJsonAsync($"/api/events/{Id}/checklist", new ChecklistItemCreateRequest {
            EventId = Id,
            SortOrder = nextSortOrder,
            ItemType = itemType,
            Label = NewItemLabel,
        });

        NewItemLabel = "";
        await SaveIfDirty();
        await LoadEvent();
    }

    private async Task OnNewItemKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(NewItemLabel))
            await AddItem(NewItemType);
    }

    // Inline edit
    private void StartEditing(EventChecklistItemDTO item) {
        EditingItemId = item.Id;
        EditingItemLabel = item.Label;
        EditingUseDescription = IsUseDescription(item.Configuration);
        EditingTemplateIdentifier = GetConfigProperty(item.Configuration, "templateIdentifier") ?? "";
        EditingEmailTargetType = GetConfigProperty(item.Configuration, "targetType") ?? "Chapter";
        EditingEmailTargetId = GetConfigProperty(item.Configuration, "targetId") ?? "";
        EditingManualAddresses = GetConfigProperty(item.Configuration, "manualAddresses") ?? "";
        EditingMotionChapterId = GetConfigProperty(item.Configuration, "chapterId") ?? "";
        EditingMotionTitle = GetConfigProperty(item.Configuration, "motionTitle") ?? "";
        EditingMotionText = GetConfigProperty(item.Configuration, "motionText") ?? "";
    }

    private void CancelEditing() {
        EditingItemId = null;
    }

    private async Task SaveEditingItem() {
        if (EditingItemId == null || Event == null)
            return;

        var item = Event.ChecklistItems.FirstOrDefault(i => i.Id == EditingItemId);
        if (item == null)
            return;

        string? config = item.Configuration;
        if (item.ItemType == 2) {
            var dict = new Dictionary<string, object> {
                ["useDescription"] = EditingUseDescription,
                ["targetType"] = EditingEmailTargetType
            };
            if (!EditingUseDescription && !string.IsNullOrWhiteSpace(EditingTemplateIdentifier)) {
                dict["templateIdentifier"] = EditingTemplateIdentifier;
            }
            if (EditingEmailTargetType == "ManualAddresses") {
                dict["manualAddresses"] = EditingManualAddresses;
            } else if (Guid.TryParse(EditingEmailTargetId, out var tid)) {
                dict["targetId"] = tid;
            }
            config = JsonSerializer.Serialize(dict);
        } else if (item.ItemType == 1) {
            var dict = new Dictionary<string, object>();
            if (Guid.TryParse(EditingMotionChapterId, out var chId))
                dict["chapterId"] = chId;
            if (!string.IsNullOrWhiteSpace(EditingMotionTitle))
                dict["motionTitle"] = EditingMotionTitle;
            dict["motionText"] = EditingMotionText;
            config = JsonSerializer.Serialize(dict);
        }

        await Http.PutAsJsonAsync($"/api/events/{Id}/checklist/{item.Id}", new ChecklistItemUpdateRequest {
            EventId = Id,
            ItemId = item.Id,
            SortOrder = item.SortOrder,
            ItemType = item.ItemType,
            Label = EditingItemLabel,
            Configuration = config
        });

        EditingItemId = null;
        PreviewCache.Clear();
        ExpandedPreviewItemId = null;
        await SaveIfDirty();
        await LoadEvent();
    }

    private async Task MoveItem(Guid itemId, int direction) {
        await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/reorder",
            new { eventId = Id, itemId, direction });
        await SaveIfDirty();
        await LoadEvent();
    }

    private async Task DeleteChecklistItem(Guid itemId) {
        await Http.DeleteAsync($"/api/events/{Id}/checklist/{itemId}");
        PreviewCache.Remove(itemId);
        if (ExpandedPreviewItemId == itemId)
            ExpandedPreviewItemId = null;
        await SaveIfDirty();
        await LoadEvent();
    }

    private async Task ToggleArchive() {
        await SaveIfDirty();
        await Http.PostAsync($"/api/events/{Id}/archive", null);
        await LoadEvent();
    }

    private async Task ToggleEmailPreview(Guid itemId, string? configuration) {
        if (ExpandedPreviewItemId == itemId) {
            ExpandedPreviewItemId = null;
            StateHasChanged();
            return;
        }

        ExpandedPreviewItemId = itemId;

        if (!PreviewCache.ContainsKey(itemId)) {
            try {
                string templateContent;
                if (IsUseDescription(configuration)) {
                    templateContent = Event?.Description ?? "(Keine Beschreibung)";
                    templateContent = ReplaceEventDateVariables(templateContent);
                } else {
                    var templateId = GetConfigProperty(configuration, "templateIdentifier");
                    if (!string.IsNullOrEmpty(templateId)) {
                        templateContent = $"*Vorlage:* `{templateId}`\n\nHallo **{{{{ member.FirstName }}}}**,\n\n(Vorschau mit Beispieldaten)";
                    } else {
                        // No template configured yet — show hint
                        PreviewCache[itemId] = "<p class=\"text-secondary\">Kein Template konfiguriert. Bearbeiten Sie den Eintrag, um ein Template oder die Beschreibung als Inhalt auszuwählen.</p>";
                        StateHasChanged();
                        return;
                    }
                }

                var mockData = TemplateMockDataProvider.GetMockData("MemberDetailDTO");
                var (html, error) = await TemplateRenderer.RenderAsync(templateContent, mockData);
                PreviewCache[itemId] = html ?? $"<p class=\"text-danger\">{error}</p>";
            } catch (Exception ex) {
                PreviewCache[itemId] = $"<p class=\"text-secondary\">Vorschau nicht verfügbar: {ex.Message}</p>";
            }
        }

        StateHasChanged();
    }

    private string ReplaceEventDateVariables(string text) {
        var dateStr = Event?.EventDate?.ToString("dd.MM.yyyy") ?? "(kein Datum)";
        text = text.Replace("{{date}}", dateStr);
        text = text.Replace("{{datum}}", dateStr);
        return text;
    }

    private static bool IsUseDescription(string? configuration) {
        if (string.IsNullOrEmpty(configuration))
            return false;
        try {
            var config = JsonSerializer.Deserialize<JsonElement>(configuration);
            if (config.TryGetProperty("useDescription", out var val))
                return val.GetBoolean();
        } catch { }
        return false;
    }

    private static string? GetConfigProperty(string? configuration, string property) {
        if (string.IsNullOrEmpty(configuration))
            return null;
        try {
            var config = JsonSerializer.Deserialize<JsonElement>(configuration);
            if (config.TryGetProperty(property, out var val))
                return val.ToString();
        } catch { }
        return null;
    }

    private static string RenderMarkdown(string markdown)
        => Markdown.ToHtml(markdown, MarkdownPipeline);
}
