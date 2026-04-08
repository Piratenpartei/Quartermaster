using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Renders a meeting's protocol as a PDF using QuestPDF. Structure matches the markdown
/// output from <see cref="ProtocolRenderer"/> but with proper typography and page breaks.
/// </summary>
public static class ProtocolPdfRenderer {
    public static byte[] Render(MeetingDetailDTO meeting) {
        var document = Document.Create(container => {
            container.Page(page => {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                page.Header().Element(h => ComposeHeader(h, meeting));
                page.Content().Element(c => ComposeContent(c, meeting));
                page.Footer().AlignCenter().Text(t => {
                    t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium));
                    t.Span("Seite ");
                    t.CurrentPageNumber();
                    t.Span(" von ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, MeetingDetailDTO meeting) {
        container.PaddingBottom(20).Column(col => {
            col.Item().Text(meeting.Title).FontSize(20).Bold();
            col.Item().PaddingTop(4).Text(t => {
                t.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken2));
                t.Span("Gliederung: ").SemiBold();
                t.Span(meeting.ChapterName);
                if (meeting.MeetingDate.HasValue) {
                    t.Span("  •  ");
                    t.Span("Datum: ").SemiBold();
                    t.Span(meeting.MeetingDate.Value.ToString("dd.MM.yyyy HH:mm"));
                }
                if (!string.IsNullOrWhiteSpace(meeting.Location)) {
                    t.Span("  •  ");
                    t.Span("Ort: ").SemiBold();
                    t.Span(meeting.Location);
                }
            });
            if (meeting.StartedAt.HasValue || meeting.CompletedAt.HasValue) {
                col.Item().PaddingTop(2).Text(t => {
                    t.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken2));
                    if (meeting.StartedAt.HasValue) {
                        t.Span("Beginn: ").SemiBold();
                        t.Span(meeting.StartedAt.Value.ToString("HH:mm"));
                    }
                    if (meeting.CompletedAt.HasValue) {
                        if (meeting.StartedAt.HasValue) t.Span("  •  ");
                        t.Span("Ende: ").SemiBold();
                        t.Span(meeting.CompletedAt.Value.ToString("HH:mm"));
                    }
                });
            }
            col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeContent(IContainer container, MeetingDetailDTO meeting) {
        container.PaddingVertical(10).Column(col => {
            col.Spacing(12);

            if (!string.IsNullOrWhiteSpace(meeting.Description)) {
                col.Item().Text(meeting.Description);
            }

            col.Item().PaddingTop(4).Text("Tagesordnung").FontSize(14).Bold();

            var rootItems = meeting.AgendaItems
                .Where(a => a.ParentId == null)
                .OrderBy(a => a.SortOrder)
                .ToList();
            var childrenByParent = meeting.AgendaItems
                .Where(a => a.ParentId != null)
                .GroupBy(a => a.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.SortOrder).ToList());

            for (var i = 0; i < rootItems.Count; i++) {
                var idx = i;
                col.Item().Element(e => RenderItem(e, rootItems[idx], (idx + 1).ToString(), 0, childrenByParent));
            }
        });
    }

    private static void RenderItem(
        IContainer container,
        AgendaItemDTO item,
        string number,
        int depth,
        Dictionary<System.Guid, List<AgendaItemDTO>> childrenByParent) {
        container.PaddingLeft(depth * 12).Column(col => {
            col.Spacing(4);

            // Numbered title
            var titleSize = depth == 0 ? 13 : (depth == 1 ? 12 : 11);
            col.Item().Text(t => {
                t.Span($"{number}  ").FontSize(titleSize).Bold();
                t.Span(item.Title).FontSize(titleSize).Bold();
            });

            // Type label
            col.Item().Text(ItemTypeLabel(item.ItemType))
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);

            // Motion block
            if (item.ItemType == AgendaItemType.Motion && !string.IsNullOrWhiteSpace(item.MotionTitle)) {
                col.Item().PaddingTop(4).Text(t => {
                    t.Span("Antrag: ").SemiBold();
                    t.Span(item.MotionTitle);
                });
                if (item.MotionVoteApproveCount + item.MotionVoteDenyCount + item.MotionVoteAbstainCount > 0) {
                    col.Item().Text(t => {
                        t.Span("Abstimmung: ").SemiBold();
                        t.Span($"{item.MotionVoteApproveCount} Ja / {item.MotionVoteDenyCount} Nein / {item.MotionVoteAbstainCount} Enthaltungen");
                    });
                }
            }

            // Notes
            if (!string.IsNullOrWhiteSpace(item.Notes)) {
                col.Item().PaddingTop(4).Text(item.Notes).FontSize(10);
            }

            // Resolution
            if (!string.IsNullOrWhiteSpace(item.Resolution)) {
                col.Item().PaddingTop(4).Background(Colors.Grey.Lighten4).Padding(6).Text(t => {
                    t.Span("Beschluss: ").SemiBold();
                    t.Span(item.Resolution);
                });
            }

            // Children
            if (childrenByParent.TryGetValue(item.Id, out var kids)) {
                for (var i = 0; i < kids.Count; i++) {
                    var idx = i;
                    var childNumber = number + "." + (idx + 1);
                    col.Item().PaddingTop(6).Element(e =>
                        RenderItem(e, kids[idx], childNumber, depth + 1, childrenByParent));
                }
            }
        });
    }

    private static string ItemTypeLabel(AgendaItemType type) => type switch {
        AgendaItemType.Discussion => "Diskussion",
        AgendaItemType.Motion => "Antrag",
        AgendaItemType.Protocol => "Protokoll",
        AgendaItemType.Break => "Pause",
        AgendaItemType.Information => "Information",
        AgendaItemType.Section => "Abschnitt",
        AgendaItemType.Presence => "Anwesenheit",
        _ => type.ToString()
    };
}
