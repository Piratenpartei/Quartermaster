using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionInquiry {
    public const string TableName = "AssemblyMotionInquiries";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public string QuestionMarkdown { get; set; } = "";
    public string QuestionHtml { get; set; } = "";
    public Guid AskedByUserId { get; set; }
    public DateTime AskedAt { get; set; }
    public DateTime ResponseDueAt { get; set; }
    public string? AnswerMarkdown { get; set; }
    public string? AnswerHtml { get; set; }
    public Guid? AnsweredByMemberId { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
}
