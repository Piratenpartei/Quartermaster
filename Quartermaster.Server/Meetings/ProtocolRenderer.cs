using System.Collections.Generic;
using System.Linq;
using System.Text;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Renders a meeting's protocol as Markdown. Walks the agenda tree recursively with
/// hierarchical numbering. Used both as the direct markdown export and as the input
/// to the HTML preview and PDF renderer.
/// </summary>
public static class ProtocolRenderer {
    public static string RenderMarkdown(MeetingDetailDTO meeting) {
        var sb = new StringBuilder();

        sb.Append("# ").AppendLine(meeting.Title);
        sb.AppendLine();
        sb.Append("**Gliederung:** ").AppendLine(meeting.ChapterName);
        if (meeting.MeetingDate.HasValue) {
            sb.Append("**Datum:** ").AppendLine(meeting.MeetingDate.Value.ToString("dd.MM.yyyy HH:mm"));
        }
        if (!string.IsNullOrWhiteSpace(meeting.Location)) {
            sb.Append("**Ort:** ").AppendLine(meeting.Location);
        }
        if (meeting.StartedAt.HasValue) {
            sb.Append("**Beginn:** ").AppendLine(meeting.StartedAt.Value.ToString("HH:mm"));
        }
        if (meeting.CompletedAt.HasValue) {
            sb.Append("**Ende:** ").AppendLine(meeting.CompletedAt.Value.ToString("HH:mm"));
        }
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(meeting.Description)) {
            sb.AppendLine(meeting.Description);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Tagesordnung");
        sb.AppendLine();

        var rootItems = meeting.AgendaItems
            .Where(a => a.ParentId == null)
            .OrderBy(a => a.SortOrder)
            .ToList();

        var childrenByParent = meeting.AgendaItems
            .Where(a => a.ParentId != null)
            .GroupBy(a => a.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.SortOrder).ToList());

        for (var i = 0; i < rootItems.Count; i++) {
            RenderItem(sb, rootItems[i], (i + 1).ToString(), 2, childrenByParent);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void RenderItem(
        StringBuilder sb,
        AgendaItemDTO item,
        string number,
        int headingLevel,
        Dictionary<System.Guid, List<AgendaItemDTO>> childrenByParent) {
        // Heading with hierarchical number
        sb.Append(new string('#', headingLevel))
          .Append(' ').Append(number).Append(" — ").AppendLine(item.Title);

        // Type label
        sb.Append('*').Append('(').Append(ItemTypeLabel(item.ItemType)).Append(')').AppendLine("*");
        sb.AppendLine();

        // Motion block
        if (item.ItemType == AgendaItemType.Motion && !string.IsNullOrWhiteSpace(item.MotionTitle)) {
            sb.Append("**Antrag:** ").AppendLine(item.MotionTitle);
            var approve = item.MotionVoteApproveCount;
            var deny = item.MotionVoteDenyCount;
            var abstain = item.MotionVoteAbstainCount;
            if (approve + deny + abstain > 0) {
                sb.AppendLine();
                sb.Append("**Abstimmung:** ")
                  .Append(approve).Append(" Ja / ")
                  .Append(deny).Append(" Nein / ")
                  .Append(abstain).AppendLine(" Enthaltungen");
            }
            sb.AppendLine();
        }

        // Notes (minute-taker discussion notes)
        if (!string.IsNullOrWhiteSpace(item.Notes)) {
            sb.AppendLine(item.Notes);
            sb.AppendLine();
        }

        // Resolution
        if (!string.IsNullOrWhiteSpace(item.Resolution)) {
            sb.Append("**Beschluss:** ").AppendLine(item.Resolution);
            sb.AppendLine();
        }

        // Recurse for children
        if (childrenByParent.TryGetValue(item.Id, out var kids)) {
            for (var i = 0; i < kids.Count; i++) {
                var childNumber = number + "." + (i + 1);
                var childLevel = System.Math.Min(headingLevel + 1, 6);
                RenderItem(sb, kids[i], childNumber, childLevel, childrenByParent);
            }
        }
    }

    private static string ItemTypeLabel(AgendaItemType type) => type switch {
        AgendaItemType.Discussion => "Diskussion",
        AgendaItemType.Motion => "Antrag",
        AgendaItemType.Protocol => "Protokoll",
        AgendaItemType.Break => "Pause",
        AgendaItemType.Information => "Information",
        _ => type.ToString()
    };
}
