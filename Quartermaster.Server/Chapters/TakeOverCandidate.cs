using System;

namespace Quartermaster.Server.Chapters;

public record TakeOverCandidate(Guid ChapterId, string ChapterName, int? PeriodDays, bool HandlesApplications);
