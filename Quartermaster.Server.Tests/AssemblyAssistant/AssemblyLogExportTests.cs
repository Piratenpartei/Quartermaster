using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Export;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class AssemblyLogExportTests {
    private const string Translations = """
        {
          "ui.assembly_log.column.time": "Zeit",
          "ui.assembly_log.column.kind": "Art",
          "ui.assembly_log.kind.speech": "Rede",
          "ui.assembly_log.kind.poll": "Abstimmung",
          "ui.assembly_log.kind.chair_action": "Versammlungsleitung",
          "ui.assembly_log.markdown_title": "Protokollhilfe: {event}",
          "ui.assembly_log.markdown_session": "Versammlung am {date}, {start} bis {end} Uhr",
          "ui.assembly_log.no_item": "Ohne Tagesordnungspunkt",
          "ui.assembly_log.speech_line": "{role}: {name}, {duration} von {allotted}",
          "ui.assembly_console.role_contra": "Contra",
          "ui.assembly_console.notice.gave_floor": "Rederecht an {name}",
          "ui.assembly_poll_result.accepted": "Angenommen ({yes} zu {no})"
        }
        """;

    // 28.11.2026 is in winter time: Berlin = UTC + 1.
    private static readonly DateTime T0 = new(2026, 11, 28, 9, 0, 0, DateTimeKind.Utc);

    private static readonly AssemblySession Session = new() { EventId = Guid.NewGuid(), StartedAt = T0, EndedAt = T0.AddHours(8), StartedByUserId = Guid.NewGuid() };

    private static AssemblyLogEntry[] Entries() {
        return [
            new AssemblyLogEntry { At = T0.AddMinutes(5), Kind = AssemblyLogKind.ChairAction, ItemTitle = "SÄA001: Titel", Detail = "ui.assembly_console.notice.gave_floor?name=Anna" },
            new AssemblyLogEntry {
                At = T0.AddMinutes(6), Kind = AssemblyLogKind.Speech, ItemTitle = "SÄA001: Titel", MumbleName = "Anna; die Erste", Role = nameof(SpeakerRole.Contra),
                StartedAt = T0.AddMinutes(6), EndedAt = T0.AddMinutes(8).AddSeconds(31), AllottedSeconds = 180
            },
            new AssemblyLogEntry { At = T0.AddMinutes(2), Kind = AssemblyLogKind.ChairAction, Detail = "ui.assembly_console.notice.gave_floor?name=Bert" },
            new AssemblyLogEntry {
                At = T0.AddMinutes(20), Kind = AssemblyLogKind.Poll, ItemTitle = "SÄA001: Titel", OpenSlidesPollId = 8,
                Detail = "Schlussabstimmung SÄA001\nui.assembly_poll_result.accepted?yes=30&no=10"
            }
        ];
    }

    [Test]
    public async Task Csv_has_bom_and_berlin_times() {
        var bytes = new AssemblyLogExport(new I18nService(Translations)).ToCsv(Entries());

        await Assert.That(bytes.Take(3).ToArray()).IsEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF });
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines[0]).StartsWith("Zeit;Art;");
        await Assert.That(lines[1]).StartsWith("2026-11-28 10:02:00;Versammlungsleitung;;;");
        var speech = lines.Single(l => l.Contains(";Rede;"));
        await Assert.That(speech).Contains("\"Anna; die Erste\";Contra;10:06:00;10:08:31;2:31;3:00;");
        await Assert.That(lines.Last()).EndsWith(";8;Schlussabstimmung SÄA001: Angenommen (30 zu 10)");
    }

    [Test]
    public async Task Csv_neutralizes_formulas_in_names() {
        AssemblyLogEntry[] entries = [
            new AssemblyLogEntry { At = T0, Kind = AssemblyLogKind.Speech, MumbleName = "=HYPERLINK(\"http://x\")", Role = nameof(SpeakerRole.Contra) },
            new AssemblyLogEntry { At = T0.AddMinutes(1), Kind = AssemblyLogKind.Speech, MumbleName = "@Bert", Role = nameof(SpeakerRole.Contra) }
        ];

        var csv = Encoding.UTF8.GetString(new AssemblyLogExport(new I18nService(Translations)).ToCsv(entries)[3..]);

        await Assert.That(csv).Contains(";\"'=HYPERLINK(\"\"http://x\"\")\";");
        await Assert.That(csv).Contains(";'@Bert;");
    }

    [Test]
    public async Task Markdown_groups_by_item() {
        var markdown = new AssemblyLogExport(new I18nService(Translations)).ToMarkdown("BPT 2026.2", Session, Entries());

        var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        await Assert.That(lines[0]).IsEqualTo("# Protokollhilfe: BPT 2026.2");
        await Assert.That(lines).Contains("Versammlung am 28.11.2026, 10:00 bis 18:00 Uhr");
        var noItem = lines.IndexOf("## Ohne Tagesordnungspunkt");
        var motion = lines.IndexOf("## SÄA001: Titel");
        await Assert.That(noItem).IsGreaterThan(0);
        await Assert.That(motion).IsGreaterThan(noItem);
        await Assert.That(lines).Contains("- 10:06:00 Contra: Anna; die Erste, 2:31 von 3:00");
        await Assert.That(lines).Contains("- 10:20:00 Abstimmung: Schlussabstimmung SÄA001: Angenommen (30 zu 10)");
        await Assert.That(lines).Contains("- 10:05:00 Versammlungsleitung: Rederecht an Anna");
    }

    [Test]
    public async Task File_name_is_safe() {
        var name = AssemblyLogExport.FileName("BPT 2026.2 (online)", Session, "csv");

        await Assert.That(name).IsEqualTo("protokollhilfe-BPT-2026-2--online-2026-11-28.csv");
    }
}
