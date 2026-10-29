using System;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Forwarding;

/// <param name="FederalId">The root chapter (F).</param>
/// <param name="StateId">The chapter directly below F in the ancestry (S), or null.</param>
/// <param name="HandlingChapterId">Nearest unmarked chapter below F, from the given chapter upwards, with an application setting (H).</param>
/// <param name="ApplicationDecidingChapterId">The chapter deciding on member administration for the given chapter, past marked chapters; null without a chapter.</param>
/// <param name="ApplicationSkip">The marked chapter <paramref name="ApplicationDecidingChapterId"/> decides for, or null.</param>
/// <param name="HandlingSkip">The first marked chapter passed over on the way to H that had an application setting, or null.</param>
/// <param name="StateSkip">The mark of S for member administration, or null; a marked S never decides.</param>
public record ForwardingChapterContext(
    Guid? FederalId,
    Guid? StateId,
    ForwardingMode? FederalApplicationMode,
    ForwardingMode? FederalReductionMode,
    ForwardingMode? StateReductionMode,
    Guid? HandlingChapterId,
    ForwardingMode? HandlingApplicationMode,
    Guid? ApplicationDecidingChapterId,
    ChapterMarkSkip? ApplicationSkip,
    ChapterMarkSkip? HandlingSkip,
    ChapterMarkSkip? StateSkip);
