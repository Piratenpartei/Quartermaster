using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionCompetitionViewBuilder {
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionCompetitionRepository _competitions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionCompetitionService _service;

    public AssemblyMotionCompetitionViewBuilder(AssemblyMotionRepository motions, AssemblyMotionCompetitionRepository competitions,
        AssemblyMotionPortalRepository portals, AssemblyMotionCompetitionService service) {
        _motions = motions;
        _competitions = competitions;
        _portals = portals;
        _service = service;
    }

    public List<AssemblyMotionCompetitionPublicDTO> BuildPublic(AssemblyMotion motion) {
        return VisibleLinks(motion)
            .Select(x => new AssemblyMotionCompetitionPublicDTO {
                Id = x.Link.Id,
                OtherMotionId = x.Other.Id,
                DisplayNumber = DisplayNumber(x.Other, x.Type) ?? "",
                Title = x.Other.Title
            })
            .ToList();
    }

    public List<AssemblyMotionCompetitionDTO> BuildDetail(AssemblyMotion motion, Member? viewer, bool isCommission) {
        return VisibleLinks(motion)
            .Select(x => new AssemblyMotionCompetitionDTO {
                Id = x.Link.Id,
                OtherMotionId = x.Other.Id,
                OtherDisplayNumber = DisplayNumber(x.Other, x.Type),
                OtherTitle = x.Other.Title,
                DeclaredByCommission = x.Link.DeclaredByCommission,
                CanRemove = _service.CanRemove(x.Link, viewer, isCommission)
            })
            .ToList();
    }

    private List<(AssemblyMotionCompetition Link, AssemblyMotion Other, AssemblyMotionType Type)> VisibleLinks(AssemblyMotion motion) {
        var result = new List<(AssemblyMotionCompetition Link, AssemblyMotion Other, AssemblyMotionType Type)>();

        foreach (var link in _competitions.ListForMotion(motion.Id)) {
            var other = _motions.Get(link.MotionAId == motion.Id ? link.MotionBId : link.MotionAId);
            if (other == null || !AssemblyMotionVisibility.IsPublic(other))
                continue;

            var type = _portals.GetType(other.TypeId);
            if (type != null)
                result.Add((link, other, type));
        }

        return result;
    }

    private static string? DisplayNumber(AssemblyMotion motion, AssemblyMotionType type) {
        return motion.Number == null ? null : AssemblyMotionNumber.Format(type.Code, motion.Number.Value);
    }
}
