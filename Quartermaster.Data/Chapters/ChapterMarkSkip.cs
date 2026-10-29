using System;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Data.Chapters;

public record ChapterMarkSkip(Guid ChapterId, ChapterMark Reason);
