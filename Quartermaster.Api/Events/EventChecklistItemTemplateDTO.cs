namespace Quartermaster.Api.Events;

public class EventChecklistItemTemplateDTO {
    public int SortOrder { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public string Label { get; set; } = "";
    public EventChecklistItemConfigDTO? Configuration { get; set; }
}
