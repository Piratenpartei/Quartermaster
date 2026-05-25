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
using Quartermaster.Api.I18n;
using Quartermaster.Api.AuditLog;
using Quartermaster.Api.Events;
using Quartermaster.Api.Rendering;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private DirtyForm _detailsForm = default!;
    private EventDetailDTO? Event;
    private bool Loading = true;
    private bool Saving;
    private bool EditingTitle;
    private List<AuditLogDTO>? AuditLogs;

    // Add item
    private string NewItemLabel { get; set; } = "";
    private ChecklistItemType NewItemType { get; set; } = ChecklistItemType.Text;

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
            AuditLogs = await Http.GetFromJsonAsync<List<AuditLogDTO>>($"/api/auditlog?entityType=Event&entityId={Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        _detailsForm?.Reset();
        StateHasChanged();
    }

    private void OnDescriptionChanged(string value) {
        if (Event != null) {
            Event.Description = value;
            _detailsForm?.MarkDirty();
        }
    }

    private void OnDateChanged(string value) {
        if (Event != null) {
            Event.EventDate = DateTime.TryParse(value, out var d) ? d : null;
        }
    }

    private void OnVisibilityChanged(string value) {
        if (Event != null && int.TryParse(value, out var v)) {
            Event.Visibility = (EventVisibility)v;
            _detailsForm?.MarkDirty();
        }
    }

    private async Task SaveDetails() {
        if (Event == null)
            return;

        Saving = true;
        StateHasChanged();

        try {
            await Http.PutAsJsonAsync($"/api/events/{Id}", new EventUpdateRequest {
                Id = Id,
                InternalName = Event.InternalName,
                PublicName = Event.PublicName,
                Description = Event.Description,
                EventDate = Event.EventDate,
                Visibility = Event.Visibility
            });
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Saving = false;
        _detailsForm.Reset();
        StateHasChanged();
    }

    private async Task SaveTitleEdit() {
        EditingTitle = false;
        _detailsForm.MarkDirty();
        await SaveDetails();
    }

    private async Task SaveIfDirty() {
        if (_detailsForm.IsDirty && Event != null) {
            try {
                await Http.PutAsJsonAsync($"/api/events/{Id}", new EventUpdateRequest {
                    Id = Id,
                    InternalName = Event.InternalName,
                    PublicName = Event.PublicName,
                    Description = Event.Description,
                    EventDate = Event.EventDate
                });
                _detailsForm.Reset();
            } catch (HttpRequestException ex) {
                ToastService.Error(ex);
            }
        }
    }

    // Checklist actions
    private async Task CheckTextItem(Guid itemId) {
        try {
            await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/check",
                new { executeAction = false });
            await SaveIfDirty();
            await LoadEvent();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task UncheckItem(Guid itemId) {
        try {
            await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/uncheck", new { });
            await SaveIfDirty();
            await LoadEvent();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CheckActionItem(Guid itemId, bool executeAction) {
        try {
            await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/check",
                new { executeAction });
            await SaveIfDirty();
            await LoadEvent();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    // Add item
    private async Task AddItem(ChecklistItemType itemType) {
        if (string.IsNullOrWhiteSpace(NewItemLabel))
            return;

        var nextSortOrder = Event?.ChecklistItems.Count ?? 0;

        try {
            await Http.PostAsJsonAsync($"/api/events/{Id}/checklist", new ChecklistItemCreateRequest {
                EventId = Id,
                SortOrder = nextSortOrder,
                ItemType = itemType,
                Label = NewItemLabel,
            });

            NewItemLabel = "";
            ToastService.ToastKey(I18nKey.Ui.Toast.ChecklistItemAdded);
            await SaveIfDirty();
            await LoadEvent();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
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
        if (item.ItemType == ChecklistItemType.SendEmail) {
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
        } else if (item.ItemType == ChecklistItemType.CreateMotion) {
            var dict = new Dictionary<string, object>();
            if (Guid.TryParse(EditingMotionChapterId, out var chId))
                dict["chapterId"] = chId;
            if (!string.IsNullOrWhiteSpace(EditingMotionTitle))
                dict["motionTitle"] = EditingMotionTitle;
            dict["motionText"] = EditingMotionText;
            config = JsonSerializer.Serialize(dict);
        }

        try {
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
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task MoveItem(Guid itemId, int direction) {
        try {
            await Http.PostAsJsonAsync($"/api/events/{Id}/checklist/{itemId}/reorder",
                new { eventId = Id, itemId, direction });
            await SaveIfDirty();
            await LoadEvent();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task DeleteChecklistItem(Guid itemId) {
        if (!await ConfirmDialog.ShowAsync(ToastService.Translate(I18nKey.Ui.Confirm.ChecklistItemDelete)))
            return;

        try {
            await Http.DeleteAsync($"/api/events/{Id}/checklist/{itemId}");
            PreviewCache.Remove(itemId);
            if (ExpandedPreviewItemId == itemId)
                ExpandedPreviewItemId = null;
            ToastService.ToastKey(I18nKey.Ui.Toast.ChecklistItemDeleted);
            await SaveIfDirty();
            await LoadEvent();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ChangeStatus(EventStatus target) {
        if (Event == null)
            return;

        var confirmKey = target switch {
            EventStatus.Archived => I18nKey.Ui.Confirm.EventArchive,
            EventStatus.Draft => I18nKey.Ui.Confirm.EventBackToDraft,
            _ => null
        };

        if (confirmKey != null && !await ConfirmDialog.ShowAsync(ToastService.Translate(confirmKey)))
            return;

        try {
            await SaveIfDirty();
            var response = await Http.PutAsJsonAsync($"/api/events/{Id}/status",
                new EventStatusUpdateRequest { Id = Id, Status = target });
            if (response.IsSuccessStatusCode) {
                var labelKey = target switch {
                    EventStatus.Draft => I18nKey.Ui.Label.EventStatusDraft,
                    EventStatus.Active => I18nKey.Ui.Label.EventStatusActive,
                    EventStatus.Completed => I18nKey.Ui.Label.EventStatusCompleted,
                    EventStatus.Archived => I18nKey.Ui.Label.EventStatusArchived,
                    _ => null
                };
                var statusLabel = labelKey != null ? ToastService.Translate(labelKey) : target.ToString();
                ToastService.Toast(
                    ToastService.Translate(I18nParams.With(I18nKey.Ui.Toast.EventStatusChanged, ("status", statusLabel))),
                    "success");
                await LoadEvent();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<(EventStatus Target, string Label, string Icon)> AllowedTransitions => Event?.Status switch {
        EventStatus.Draft => [(EventStatus.Active, "Aktivieren", "bi-play-circle")],
        EventStatus.Active => [
            (EventStatus.Draft, "Zurück zu Entwurf", "bi-arrow-counterclockwise"),
            (EventStatus.Completed, "Als abgeschlossen markieren", "bi-check-circle")
        ],
        EventStatus.Completed => [
            (EventStatus.Active, "Zurück zu Aktiv", "bi-arrow-counterclockwise"),
            (EventStatus.Archived, "Archivieren", "bi-archive")
        ],
        EventStatus.Archived => [(EventStatus.Completed, "Dearchivieren", "bi-box-arrow-up")],
        _ => []
    };

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
        } catch (JsonException ex) {
            Console.Error.WriteLine($"EventDetail.IsUseDescription: malformed config JSON. {ex}");
        }
        return false;
    }

    private static string? GetConfigProperty(string? configuration, string property) {
        if (string.IsNullOrEmpty(configuration))
            return null;
        try {
            var config = JsonSerializer.Deserialize<JsonElement>(configuration);
            if (config.TryGetProperty(property, out var val))
                return val.ToString();
        } catch (JsonException ex) {
            Console.Error.WriteLine($"EventDetail.GetConfigProperty({property}): malformed config JSON. {ex}");
        }
        return null;
    }

    private static string RenderMarkdown(string markdown)
        => Markdown.ToHtml(markdown, MarkdownPipeline);
}
