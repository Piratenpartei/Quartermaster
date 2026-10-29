using System;

namespace Quartermaster.Server.Chapters;

public record TakeOverStep(Guid ChapterId, string ChapterName, DateTime DueAt);
