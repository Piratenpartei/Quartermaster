using System;

namespace Quartermaster.Data.Chapters;

public interface IChapterIdentifier {
    Guid ChapterId { get; }
}