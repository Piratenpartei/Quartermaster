using System;

namespace Quartermaster.Server.Chapters;

public record HandoverPreview(Guid TargetChapterId, string TargetChapterName, int Count);
