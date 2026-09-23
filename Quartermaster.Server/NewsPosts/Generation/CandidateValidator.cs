using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Generation;

public static class CandidateValidator {
    public static List<GeneratedCandidate> Validate(IEnumerable<GeneratedCandidate> raw, IReadOnlyCollection<Guid> allowedSectionIds, int maxCount) {
        return raw
            .Select(c => c with { Text = c.Text.Trim() })
            .Where(c => PostText.IsValid(c.Text))
            .Where(c => allowedSectionIds.Contains(c.SectionId))
            .Take(maxCount)
            .ToList();
    }
}
