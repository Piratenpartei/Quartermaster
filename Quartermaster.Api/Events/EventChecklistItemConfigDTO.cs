using System;

namespace Quartermaster.Api.Events;

/// <summary>
/// Flat union covering the per-<see cref="ChecklistItemType"/> configuration variants:
/// <list type="bullet">
/// <item><c>SendEmail</c>: <see cref="UseDescription"/>, <see cref="TargetType"/>, <see cref="TemplateIdentifier"/>, <see cref="ManualAddresses"/>, <see cref="TargetId"/>.</item>
/// <item><c>CreateMotion</c>: <see cref="ChapterId"/>, <see cref="MotionTitle"/>, <see cref="MotionText"/>.</item>
/// <item><c>Text</c>: empty.</item>
/// </list>
/// Unused-by-variant properties stay null so JSON payloads of mixed-vintage clients round-trip cleanly.
/// </summary>
public class EventChecklistItemConfigDTO {
    public bool UseDescription { get; set; }
    public string? TargetType { get; set; }
    public string? TemplateIdentifier { get; set; }
    public string? ManualAddresses { get; set; }
    public Guid? TargetId { get; set; }

    public Guid? ChapterId { get; set; }
    public string? MotionTitle { get; set; }
    public string? MotionText { get; set; }
}
