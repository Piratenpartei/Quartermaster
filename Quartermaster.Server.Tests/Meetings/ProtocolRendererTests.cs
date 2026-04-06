using System;
using System.Threading.Tasks;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Meetings;

namespace Quartermaster.Server.Tests.Meetings;

public class ProtocolRendererTests {
    [Test]
    public async Task Renders_title_chapter_and_date() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "Vorstandssitzung März",
            ChapterName = "LV Niedersachsen",
            MeetingDate = new DateTime(2026, 3, 15, 19, 0, 0),
            Location = "Bürgerhaus Hannover"
        };
        var md = ProtocolRenderer.RenderMarkdown(meeting);
        await Assert.That(md).Contains("# Vorstandssitzung März");
        await Assert.That(md).Contains("LV Niedersachsen");
        await Assert.That(md).Contains("15.03.2026");
        await Assert.That(md).Contains("Bürgerhaus Hannover");
    }

    [Test]
    public async Task Renders_empty_agenda_section() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "Empty Meeting",
            ChapterName = "Chapter"
        };
        var md = ProtocolRenderer.RenderMarkdown(meeting);
        await Assert.That(md).Contains("## Tagesordnung");
    }

    [Test]
    public async Task Renders_agenda_items_with_hierarchical_numbering() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "M",
            ChapterName = "C",
            AgendaItems = [
                new AgendaItemDTO { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), SortOrder = 0, Title = "Begrüßung", ItemType = AgendaItemType.Discussion },
                new AgendaItemDTO { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), SortOrder = 1, Title = "Hauptpunkt", ItemType = AgendaItemType.Discussion },
                new AgendaItemDTO { Id = Guid.NewGuid(), ParentId = Guid.Parse("22222222-2222-2222-2222-222222222222"), SortOrder = 0, Title = "Unterpunkt A", ItemType = AgendaItemType.Discussion },
                new AgendaItemDTO { Id = Guid.NewGuid(), ParentId = Guid.Parse("22222222-2222-2222-2222-222222222222"), SortOrder = 1, Title = "Unterpunkt B", ItemType = AgendaItemType.Discussion }
            ]
        };
        var md = ProtocolRenderer.RenderMarkdown(meeting);
        await Assert.That(md).Contains("1 — Begrüßung");
        await Assert.That(md).Contains("2 — Hauptpunkt");
        await Assert.That(md).Contains("2.1 — Unterpunkt A");
        await Assert.That(md).Contains("2.2 — Unterpunkt B");
    }

    [Test]
    public async Task Renders_motion_item_with_vote_tally() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "M",
            ChapterName = "C",
            AgendaItems = [
                new AgendaItemDTO {
                    Id = Guid.NewGuid(), SortOrder = 0,
                    Title = "Beschluss Beiträge",
                    ItemType = AgendaItemType.Motion,
                    MotionId = Guid.NewGuid(),
                    MotionTitle = "Erhöhung Mitgliedsbeiträge 2026",
                    MotionVoteApproveCount = 5,
                    MotionVoteDenyCount = 1,
                    MotionVoteAbstainCount = 2,
                    Resolution = "Angenommen"
                }
            ]
        };
        var md = ProtocolRenderer.RenderMarkdown(meeting);
        await Assert.That(md).Contains("Erhöhung Mitgliedsbeiträge 2026");
        await Assert.That(md).Contains("5 Ja");
        await Assert.That(md).Contains("1 Nein");
        await Assert.That(md).Contains("2 Enthaltungen");
        await Assert.That(md).Contains("**Beschluss:** Angenommen");
    }

    [Test]
    public async Task Renders_unicode_content() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "Sitzung mit Sonderzeichen: äöü ß",
            ChapterName = "Gliederung",
            Description = "Grüße aus München 👋",
            AgendaItems = [
                new AgendaItemDTO { Id = Guid.NewGuid(), SortOrder = 0, Title = "TOP ä ö ü", ItemType = AgendaItemType.Discussion }
            ]
        };
        var md = ProtocolRenderer.RenderMarkdown(meeting);
        await Assert.That(md).Contains("äöü ß");
        await Assert.That(md).Contains("München 👋");
        await Assert.That(md).Contains("TOP ä ö ü");
    }
}

public class ProtocolPdfRendererTests {
    [Test]
    public async Task Produces_non_empty_pdf_bytes() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "PDF Test",
            ChapterName = "C",
            MeetingDate = DateTime.UtcNow,
            AgendaItems = [
                new AgendaItemDTO { Id = Guid.NewGuid(), SortOrder = 0, Title = "Item 1", ItemType = AgendaItemType.Discussion }
            ]
        };
        var bytes = ProtocolPdfRenderer.Render(meeting);
        await Assert.That(bytes).IsNotNull();
        await Assert.That(bytes.Length).IsGreaterThan(1000); // any real PDF is > 1KB
        // PDF magic bytes
        await Assert.That(bytes[0]).IsEqualTo((byte)'%');
        await Assert.That(bytes[1]).IsEqualTo((byte)'P');
        await Assert.That(bytes[2]).IsEqualTo((byte)'D');
        await Assert.That(bytes[3]).IsEqualTo((byte)'F');
    }

    [Test]
    public async Task Pdf_renders_with_nested_items() {
        var parentId = Guid.NewGuid();
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "Nested",
            ChapterName = "C",
            AgendaItems = [
                new AgendaItemDTO { Id = parentId, SortOrder = 0, Title = "Parent", ItemType = AgendaItemType.Discussion },
                new AgendaItemDTO { Id = Guid.NewGuid(), ParentId = parentId, SortOrder = 0, Title = "Child", ItemType = AgendaItemType.Discussion }
            ]
        };
        var bytes = ProtocolPdfRenderer.Render(meeting);
        await Assert.That(bytes.Length).IsGreaterThan(1000);
    }

    [Test]
    public async Task Pdf_renders_empty_meeting() {
        var meeting = new MeetingDetailDTO {
            Id = Guid.NewGuid(),
            Title = "Empty",
            ChapterName = "C"
        };
        var bytes = ProtocolPdfRenderer.Render(meeting);
        await Assert.That(bytes.Length).IsGreaterThan(500);
    }
}
