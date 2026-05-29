using System;

namespace Quartermaster.Api.Motions;

/// <summary>
/// Substantive edit to an existing motion. Route id supplies the target — the body carries the
/// new field values. Sent as PUT /api/motions/{id}. Per-field diff vs. the stored row drives
/// the audit log; unchanged fields produce no audit entry.
/// </summary>
public class MotionUpdateRequest {
    public Guid Id { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Markdown source. Re-rendered to HTML server-side for the stored <c>Text</c> column.</summary>
    public string TextMarkdown { get; set; } = "";

    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }
}
