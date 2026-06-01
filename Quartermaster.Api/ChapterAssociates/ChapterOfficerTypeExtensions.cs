namespace Quartermaster.Api.ChapterAssociates;

public static class ChapterOfficerTypeExtensions {
    /// <summary>Display order for officer lists: leadership pairs (chair + vice, treasurer + vice) come first,
    /// other named roles next, and "Member" (Beisitzer) always last so callers can tiebreak that group by last name.
    /// Persistence order on <see cref="ChapterOfficerType"/> is separate and must stay frozen.</summary>
    public static int SortRank(this ChapterOfficerType type) => type switch {
        ChapterOfficerType.Captain => 0,
        ChapterOfficerType.FirstOfficer => 1,
        ChapterOfficerType.Treasurer => 2,
        ChapterOfficerType.ViceTreasurer => 3,
        ChapterOfficerType.Quartermaster => 4,
        ChapterOfficerType.PoliticalDirector => 5,
        ChapterOfficerType.Member => 6,
        _ => 99
    };
}
