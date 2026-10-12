using System;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionDraft(Guid TypeId, string Title, string? Section, string TextMarkdown, string ReasonMarkdown,
    string? Summary, string? Keywords, string? PriorDiscussion);
