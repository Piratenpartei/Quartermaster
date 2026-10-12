using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionInquiryDTO {
    public Guid Id { get; set; }
    public string QuestionHtml { get; set; } = "";
    public DateTimeOffset AskedAt { get; set; }
    public DateTimeOffset ResponseDueAt { get; set; }
    public string? AnswerHtml { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public string? AnsweredByName { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public class AssemblyMotionInquiryAskRequest {
    public Guid MotionId { get; set; }
    public string QuestionMarkdown { get; set; } = "";
    public DateTimeOffset ResponseDueAt { get; set; }
}

public class AssemblyMotionInquiryAnswerRequest {
    public Guid Id { get; set; }
    public string AnswerMarkdown { get; set; } = "";
}

public class AssemblyMotionInquiryIdRequest {
    public Guid Id { get; set; }
}
