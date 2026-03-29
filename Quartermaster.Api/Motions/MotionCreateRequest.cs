using System;

namespace Quartermaster.Api.Motions;

public class MotionCreateRequest {
    public Guid ChapterId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorEMail { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}
