using System;

namespace Quartermaster.Api.AssemblyMotions;

public record AssemblyMotionNotificationModel {
    public Guid Id { get; set; }
    public string DisplayNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string TitleEscaped { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string EventName { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string CheckNote { get; set; } = "";
    public string ChangeNote { get; set; } = "";
    public string InquiryQuestion { get; set; } = "";
    public string InquiryDueAt { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string Path { get; set; } = "";
    public string OtherDisplayNumber { get; set; } = "";
    public string OtherTitle { get; set; } = "";
    public string OtherTitleEscaped { get; set; } = "";
}
