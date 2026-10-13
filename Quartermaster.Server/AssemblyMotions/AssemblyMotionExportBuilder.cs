using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionExportBuilder {
    private readonly DbContext _db;
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionUpvoteRepository _upvotes;
    private readonly AssemblyMotionCompetitionRepository _competitions;

    public AssemblyMotionExportBuilder(DbContext db, AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AssemblyMotionUpvoteRepository upvotes, AssemblyMotionCompetitionRepository competitions) {
        _db = db;
        _motions = motions;
        _portals = portals;
        _upvotes = upvotes;
        _competitions = competitions;
    }

    public AssemblyMotionExportDTO Build(AssemblyMotionPortal portal, Event ev, DateTime nowUtc) {
        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var numbered = _motions.ListNumberedByPortal(portal.Id)
            .Where(AssemblyMotionVisibility.IsPublic)
            .Where(m => m.Number != null)
            .Where(m => types.ContainsKey(m.TypeId))
            .ToList();
        var ordered = AssemblyMotionExportOrder.Sort(portal, numbered, types, _upvotes, nowUtc);
        var submitters = AssemblyMotionSubmitterNames.ForMotions(_db, _motions, numbered);
        var links = _competitions.ListForMotions(numbered.Select(m => m.Id).ToList());
        var byId = numbered.ToDictionary(m => m.Id);

        return new AssemblyMotionExportDTO {
            Portal = new AssemblyMotionExportPortalDTO {
                EventName = ev.PublicName,
                AssemblyLabel = portal.WikiAssemblyLabel
            },
            ExportedAt = new DateTimeOffset(nowUtc, TimeSpan.Zero),
            Motions = ordered
                .Select(entry => entry.Motion.ToExportItemDto(
                    entry.Type,
                    CompetingDisplayNumbers(entry.Motion, links[entry.Motion.Id], byId, types),
                    submitters[entry.Motion.Id],
                    entry.Rank,
                    entry.UpvoteCount))
                .ToList()
        };
    }

    private static List<string> CompetingDisplayNumbers(AssemblyMotion motion, List<AssemblyMotionCompetition> links,
        Dictionary<Guid, AssemblyMotion> numberedById, Dictionary<Guid, AssemblyMotionType> types) {
        return links
            .Select(link => link.MotionAId == motion.Id ? link.MotionBId : link.MotionAId)
            .Select(id => numberedById.GetValueOrDefault(id))
            .OfType<AssemblyMotion>()
            .OrderBy(other => types[other.TypeId].SortOrder)
            .ThenBy(other => other.Number)
            .Select(other => AssemblyMotionNumber.Format(types[other.TypeId].Code, other.Number!.Value))
            .ToList();
    }
}
