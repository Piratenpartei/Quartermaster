using System;

namespace Quartermaster.Api.Motions;

public class MotionCreateRequest {
    public Guid ChapterId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";

    public MotionDetailDTO ToDetailDto(string chapterName) => new() {
        ChapterId = ChapterId,
        ChapterName = chapterName,
        AuthorName = AuthorName,
        AuthorEmail = AuthorEmail,
        Title = Title,
        Text = Text
    };
}
