using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionPortal {
    public const string TableName = "AssemblyMotionPortals";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public string IntroMarkdown { get; set; } = "";
    public string IntroHtml { get; set; } = "";
    public bool PrioritizationEnabled { get; set; }
    public DateTime? PrioritizationStartsAt { get; set; }
    public DateTime? PrioritizationEndsAt { get; set; }
    public bool WikiMirrorEnabled { get; set; }
    public string? WikiPagePrefix { get; set; }
    public string? WikiAssemblyLabel { get; set; }
    public int? OpenSlidesMeetingId { get; set; }
    public bool ForumMirrorEnabled { get; set; }
    public int? ForumCategoryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AssemblyMotionPortalDTO ToDto(string eventName, DateTime? eventDate, IEnumerable<AssemblyMotionType> types) {
        return new() {
            Id = Id,
            EventId = EventId,
            EventName = eventName,
            EventDate = eventDate.ToDtoDate(),
            OpensAt = OpensAt.ToDtoUtc(),
            ClosesAt = ClosesAt.ToDtoUtc(),
            IntroHtml = IntroHtml,
            PrioritizationEnabled = PrioritizationEnabled,
            PrioritizationStartsAt = PrioritizationStartsAt.ToDtoUtc(),
            PrioritizationEndsAt = PrioritizationEndsAt.ToDtoUtc(),
            Types = types.Select(t => t.ToDto()).ToList()
        };
    }

    public AssemblyMotionPortalSettingsDTO ToSettingsDto(string eventName, DateTime? eventDate, IEnumerable<AssemblyMotionType> types, IReadOnlySet<Guid> typeIdsWithMotions) {
        return new() {
            Id = Id,
            EventId = EventId,
            EventName = eventName,
            EventDate = eventDate.ToDtoDate(),
            OpensAt = OpensAt.ToDtoUtc(),
            ClosesAt = ClosesAt.ToDtoUtc(),
            IntroMarkdown = IntroMarkdown,
            IntroHtml = IntroHtml,
            PrioritizationEnabled = PrioritizationEnabled,
            PrioritizationStartsAt = PrioritizationStartsAt.ToDtoUtc(),
            PrioritizationEndsAt = PrioritizationEndsAt.ToDtoUtc(),
            WikiMirrorEnabled = WikiMirrorEnabled,
            WikiPagePrefix = WikiPagePrefix,
            WikiAssemblyLabel = WikiAssemblyLabel,
            OpenSlidesMeetingId = OpenSlidesMeetingId,
            ForumMirrorEnabled = ForumMirrorEnabled,
            ForumCategoryId = ForumCategoryId,
            Types = types.Select(t => t.ToSettingsDto(typeIdsWithMotions.Contains(t.Id))).ToList()
        };
    }
}
