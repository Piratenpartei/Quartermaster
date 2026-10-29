using System;

namespace Quartermaster.Api.Chapters;

public class ChapterMarksRequest {
    public Guid Id { get; set; }
    public bool NoMemberAdministration { get; set; }
    public bool UnableToAct { get; set; }
}
