using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;
using Quartermaster.Rendering;

namespace Quartermaster.Blazor.Components.Events;

public partial class EventChecklistEditor {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public required Guid EventId { get; set; }

    [Parameter]
    public required EventDetailDTO Event { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public EventCallback OnBeforeAction { get; set; }

    private ConfirmDialog ConfirmDialog = default!;

    private string NewItemLabel { get; set; } = "";
    private ChecklistItemType NewItemType { get; set; } = ChecklistItemType.Text;

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

    private Guid? ExpandedPreviewItemId;
    private Dictionary<Guid, string?> PreviewCache = new();

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private async Task NotifyChanged() {
        if (OnChanged.HasDelegate)
            await OnChanged.InvokeAsync();
    }

    private async Task RunBeforeAction() {
        if (OnBeforeAction.HasDelegate)
            await OnBeforeAction.InvokeAsync();
    }

    private async Task CheckTextItem(Guid itemId) {
        try {
            await RunBeforeAction();
            await Http.PostAsJsonAsync($"/api/events/{EventId}/checklist/{itemId}/check",
                new { executeAction = false });
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task UncheckItem(Guid itemId) {
        try {
            await RunBeforeAction();
            await Http.PostAsJsonAsync($"/api/events/{EventId}/checklist/{itemId}/uncheck", new { });
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CheckActionItem(Guid itemId, bool executeAction) {
        try {
            await RunBeforeAction();
            await Http.PostAsJsonAsync($"/api/events/{EventId}/checklist/{itemId}/check",
                new { executeAction });
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task AddItem(ChecklistItemType itemType) {
        if (string.IsNullOrWhiteSpace(NewItemLabel))
            return;

        var nextSortOrder = Event.ChecklistItems.Count;

        try {
            await RunBeforeAction();
            await Http.PostAsJsonAsync($"/api/events/{EventId}/checklist", new ChecklistItemCreateRequest {
                EventId = EventId,
                SortOrder = nextSortOrder,
                ItemType = itemType,
                Label = NewItemLabel,
            });

            NewItemLabel = "";
            ToastService.ToastKey(I18nKey.Ui.Toast.ChecklistItemAdded);
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task OnNewItemKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(NewItemLabel))
            await AddItem(NewItemType);
    }

    private void StartEditing(EventChecklistItemDTO item) {
        EditingItemId = item.Id;
        EditingItemLabel = item.Label;
        var cfg = item.Configuration;
        EditingUseDescription = cfg?.UseDescription ?? false;
        EditingTemplateIdentifier = cfg?.TemplateIdentifier ?? "";
        EditingEmailTargetType = cfg?.TargetType ?? "Chapter";
        EditingEmailTargetId = cfg?.TargetId?.ToString() ?? "";
        EditingManualAddresses = cfg?.ManualAddresses ?? "";
        EditingMotionChapterId = cfg?.ChapterId?.ToString() ?? "";
        EditingMotionTitle = cfg?.MotionTitle ?? "";
        EditingMotionText = cfg?.MotionText ?? "";
    }

    private void CancelEditing() {
        EditingItemId = null;
    }

    private async Task SaveEditingItem() {
        if (EditingItemId == null)
            return;

        var item = Event.ChecklistItems.FirstOrDefault(i => i.Id == EditingItemId);
        if (item == null)
            return;

        var config = item.Configuration;
        if (item.ItemType == ChecklistItemType.SendEmail) {
            config = new EventChecklistItemConfigDTO {
                UseDescription = EditingUseDescription,
                TargetType = EditingEmailTargetType,
                TemplateIdentifier = !EditingUseDescription && !string.IsNullOrWhiteSpace(EditingTemplateIdentifier)
                    ? EditingTemplateIdentifier
                    : null,
                ManualAddresses = EditingEmailTargetType == "ManualAddresses" ? EditingManualAddresses : null,
                TargetId = EditingEmailTargetType != "ManualAddresses" && Guid.TryParse(EditingEmailTargetId, out var tid)
                    ? tid
                    : null
            };
        } else if (item.ItemType == ChecklistItemType.CreateMotion) {
            config = new EventChecklistItemConfigDTO {
                ChapterId = Guid.TryParse(EditingMotionChapterId, out var chId) ? chId : null,
                MotionTitle = !string.IsNullOrWhiteSpace(EditingMotionTitle) ? EditingMotionTitle : null,
                MotionText = EditingMotionText
            };
        }

        try {
            await RunBeforeAction();
            await Http.PutAsJsonAsync($"/api/events/{EventId}/checklist/{item.Id}", new ChecklistItemUpdateRequest {
                EventId = EventId,
                ItemId = item.Id,
                SortOrder = item.SortOrder,
                ItemType = item.ItemType,
                Label = EditingItemLabel,
                Configuration = config
            });

            EditingItemId = null;
            PreviewCache.Clear();
            ExpandedPreviewItemId = null;
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task MoveItem(Guid itemId, int direction) {
        try {
            await RunBeforeAction();
            await Http.PostAsJsonAsync($"/api/events/{EventId}/checklist/{itemId}/reorder",
                new { eventId = EventId, itemId, direction });
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task DeleteChecklistItem(Guid itemId) {
        if (!await ConfirmDialog.ShowAsync(ToastService.Translate(I18nKey.Ui.Confirm.ChecklistItemDelete)))
            return;

        try {
            await RunBeforeAction();
            await Http.DeleteAsync($"/api/events/{EventId}/checklist/{itemId}");
            PreviewCache.Remove(itemId);
            if (ExpandedPreviewItemId == itemId)
                ExpandedPreviewItemId = null;
            ToastService.ToastKey(I18nKey.Ui.Toast.ChecklistItemDeleted);
            await NotifyChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ToggleEmailPreview(Guid itemId, EventChecklistItemConfigDTO? configuration) {
        if (ExpandedPreviewItemId == itemId) {
            ExpandedPreviewItemId = null;
            StateHasChanged();
            return;
        }

        ExpandedPreviewItemId = itemId;

        if (!PreviewCache.ContainsKey(itemId)) {
            try {
                string templateContent;
                if (configuration?.UseDescription == true) {
                    templateContent = Event.Description ?? "(Keine Beschreibung)";
                    templateContent = ReplaceEventDateVariables(templateContent);
                } else if (!string.IsNullOrEmpty(configuration?.TemplateIdentifier)) {
                    templateContent = $"*Vorlage:* `{configuration.TemplateIdentifier}`\n\nHallo **{{{{ member.FirstName }}}}**,\n\n(Vorschau mit Beispieldaten)";
                } else {
                    PreviewCache[itemId] = "<p class=\"text-secondary\">Kein Template konfiguriert. Bearbeiten Sie den Eintrag, um ein Template oder die Beschreibung als Inhalt auszuwählen.</p>";
                    StateHasChanged();
                    return;
                }

                var mockData = TemplateMockDataProvider.GetMockData("MemberDetailDTO");
                var (html, error) = await TemplateRenderer.RenderHtmlAsync(templateContent, mockData);
                PreviewCache[itemId] = html ?? $"<p class=\"text-danger\">{error}</p>";
            } catch (Exception ex) {
                PreviewCache[itemId] = $"<p class=\"text-secondary\">Vorschau nicht verfügbar: {ex.Message}</p>";
            }
        }

        StateHasChanged();
    }

    private string ReplaceEventDateVariables(string text) {
        var dateStr = Event.EventDate?.ToString("dd.MM.yyyy") ?? "(kein Datum)";
        text = text.Replace("{{date}}", dateStr);
        text = text.Replace("{{datum}}", dateStr);
        return text;
    }

    private static string RenderMarkdown(string markdown)
        => Markdown.ToHtml(markdown, MarkdownPipeline);
}
