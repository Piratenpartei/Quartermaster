using System;

namespace Quartermaster.Data.Chapters;

public record ChapterMarkRoute(Guid DecidingChapterId, ChapterMarkSkip? Skip);
