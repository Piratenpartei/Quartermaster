using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Helpers for mapping Meeting / AgendaItem entities to their DTO shapes.
/// Uses batch lookups for motion titles + vote tallies so detail renders stay O(1) in
/// the agenda size.
/// </summary>
public static class MeetingDtoBuilder {
    public static MeetingDTO BuildMeetingDTO(Meeting meeting, string chapterName, int agendaItemCount) {
        return new MeetingDTO {
            Id = meeting.Id,
            ChapterId = meeting.ChapterId,
            ChapterName = chapterName,
            Title = meeting.Title,
            MeetingDate = meeting.MeetingDate,
            Status = meeting.Status,
            Visibility = meeting.Visibility,
            Location = meeting.Location,
            AgendaItemCount = agendaItemCount
        };
    }

    public static MeetingDetailDTO BuildMeetingDetailDTO(
        Meeting meeting,
        string chapterName,
        List<AgendaItem> agendaItems,
        Dictionary<Guid, Motion> motionsById,
        Dictionary<Guid, (int Approve, int Deny, int Abstain)> voteTalliesByMotionId) {

        var itemDtos = agendaItems
            .OrderBy(a => a.ParentId.HasValue ? 1 : 0)
            .ThenBy(a => a.ParentId)
            .ThenBy(a => a.SortOrder)
            .Select(a => BuildAgendaItemDTO(a, motionsById, voteTalliesByMotionId))
            .ToList();

        return new MeetingDetailDTO {
            Id = meeting.Id,
            ChapterId = meeting.ChapterId,
            ChapterName = chapterName,
            Title = meeting.Title,
            MeetingDate = meeting.MeetingDate,
            Status = meeting.Status,
            Visibility = meeting.Visibility,
            Location = meeting.Location,
            Description = meeting.Description,
            StartedAt = meeting.StartedAt,
            CompletedAt = meeting.CompletedAt,
            ArchivedPdfPath = meeting.ArchivedPdfPath,
            AgendaItems = itemDtos
        };
    }

    public static AgendaItemDTO BuildAgendaItemDTO(
        AgendaItem item,
        Dictionary<Guid, Motion> motionsById,
        Dictionary<Guid, (int Approve, int Deny, int Abstain)> voteTalliesByMotionId,
        List<AgendaItemOfficerVoteDTO>? officerVotes = null) {

        string? motionTitle = null;
        int? motionApprovalStatus = null;
        int approveCount = 0;
        int denyCount = 0;
        int abstainCount = 0;

        if (item.MotionId.HasValue && motionsById.TryGetValue(item.MotionId.Value, out var motion)) {
            motionTitle = motion.Title;
            motionApprovalStatus = (int)motion.ApprovalStatus;
            if (voteTalliesByMotionId.TryGetValue(item.MotionId.Value, out var tally)) {
                approveCount = tally.Approve;
                denyCount = tally.Deny;
                abstainCount = tally.Abstain;
            }
        }

        return new AgendaItemDTO {
            Id = item.Id,
            ParentId = item.ParentId,
            SortOrder = item.SortOrder,
            Title = item.Title,
            ItemType = item.ItemType,
            MotionId = item.MotionId,
            MotionTitle = motionTitle,
            MotionApprovalStatus = motionApprovalStatus,
            MotionVoteApproveCount = approveCount,
            MotionVoteDenyCount = denyCount,
            MotionVoteAbstainCount = abstainCount,
            Notes = item.Notes,
            Resolution = item.Resolution,
            StartedAt = item.StartedAt,
            CompletedAt = item.CompletedAt,
            OfficerVotes = officerVotes
        };
    }
}
