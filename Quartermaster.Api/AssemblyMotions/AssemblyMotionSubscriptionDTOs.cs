using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionSubscriptionListItemDTO {
    public Guid MotionId { get; set; }

    /// <summary>False when the motion was deleted or the viewer can no longer see its event; title, event name and status are then left empty.</summary>
    public bool IsVisible { get; set; }
    public string Title { get; set; } = "";
    public string EventName { get; set; } = "";
    public AssemblyMotionStatus? Status { get; set; }
}
