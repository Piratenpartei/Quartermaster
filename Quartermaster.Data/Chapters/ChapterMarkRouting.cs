using System;
using System.Collections.Generic;

namespace Quartermaster.Data.Chapters;

public static class ChapterMarkRouting {
    /// <summary>The root always decides, even when marked.</summary>
    public static ChapterMarkRoute Resolve(IReadOnlyList<Chapter> chainFromChapterToRoot, ChapterMarkPurpose purpose) {
        if (chainFromChapterToRoot.Count == 0)
            throw new ArgumentException("The chapter chain must not be empty.", nameof(chainFromChapterToRoot));

        ChapterMarkSkip? skip = null;
        for (var i = 0; i < chainFromChapterToRoot.Count - 1; i++) {
            var chapter = chainFromChapterToRoot[i];
            var mark = chapter.MarkFor(purpose);
            if (mark == null)
                return new ChapterMarkRoute(chapter.Id, skip);
            skip ??= new ChapterMarkSkip(chapter.Id, mark.Value);
        }
        return new ChapterMarkRoute(chainFromChapterToRoot[^1].Id, skip);
    }
}
