using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <param name="MatchedName">The other name the division was found by; null when its shown name matched.</param>
public record DivisionSearchHit(Guid Id, string? MatchedName);

public record DivisionSearchResult(List<DivisionSearchHit> Items, int TotalCount);
