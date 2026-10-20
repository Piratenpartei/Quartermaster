using System;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Server.Forwarding;

/// <param name="FederalId">The root chapter (F).</param>
/// <param name="StateId">The chapter directly below F in the ancestry (S), or null.</param>
/// <param name="HandlingChapterId">Nearest chapter below F, from the given chapter upwards, with an application setting (H).</param>
public record ForwardingChapterContext(
    Guid? FederalId,
    Guid? StateId,
    ForwardingMode? FederalApplicationMode,
    ForwardingMode? FederalReductionMode,
    ForwardingMode? StateReductionMode,
    Guid? HandlingChapterId,
    ForwardingMode? HandlingApplicationMode);
