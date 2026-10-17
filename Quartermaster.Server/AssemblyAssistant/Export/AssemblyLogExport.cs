using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Export;

/// <summary>
/// The session log as an aid for the minutes: CSV for spreadsheets and Markdown grouped by agenda item. Times are Berlin
/// time; stored i18n keys (chair actions, poll results) are translated with the server's German texts.
/// </summary>
public class AssemblyLogExport {
    private readonly I18nService _i18n;

    public AssemblyLogExport(I18nService i18n) {
        _i18n = i18n;
    }

    /// <summary>Semicolon-separated with a UTF-8 BOM, so spreadsheet programs open it with umlauts intact.</summary>
    public byte[] ToCsv(IReadOnlyList<AssemblyLogEntry> entries) {
        var builder = new StringBuilder();
        AppendRow(builder, [
            I18nKey.Ui.AssemblyLog.ColumnTime, I18nKey.Ui.AssemblyLog.ColumnKind, I18nKey.Ui.AssemblyLog.ColumnItem, I18nKey.Ui.AssemblyLog.ColumnName,
            I18nKey.Ui.AssemblyLog.ColumnRole, I18nKey.Ui.AssemblyLog.ColumnStart, I18nKey.Ui.AssemblyLog.ColumnEnd, I18nKey.Ui.AssemblyLog.ColumnDuration,
            I18nKey.Ui.AssemblyLog.ColumnAllotted, I18nKey.Ui.AssemblyLog.ColumnPoll, I18nKey.Ui.AssemblyLog.ColumnDetail
        ], translate: true);

        foreach (var entry in entries.OrderBy(e => e.At)) {
            AppendRow(builder, [
                Time(entry.At, "yyyy-MM-dd HH:mm:ss"),
                _i18n[KindKey(entry.Kind)],
                entry.ItemTitle ?? "",
                entry.MumbleName ?? "",
                RoleLabel(entry),
                entry.StartedAt == null ? "" : Time(entry.StartedAt.Value, "HH:mm:ss"),
                entry.EndedAt == null ? "" : Time(entry.EndedAt.Value, "HH:mm:ss"),
                Duration(entry) ?? "",
                entry.AllottedSeconds == null ? "" : Minutes(entry.AllottedSeconds.Value),
                entry.OpenSlidesPollId?.ToString() ?? "",
                DetailText(entry)
            ], translate: false);
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public string ToMarkdown(string eventName, AssemblySession session, IReadOnlyList<AssemblyLogEntry> entries) {
        var builder = new StringBuilder();
        builder.AppendLine($"# {_i18n[I18nParams.With(I18nKey.Ui.AssemblyLog.MarkdownTitle, ("event", eventName))]}");
        builder.AppendLine();
        builder.AppendLine(_i18n[I18nParams.With(I18nKey.Ui.AssemblyLog.MarkdownSession,
            ("date", Time(session.StartedAt, "dd.MM.yyyy")), ("start", Time(session.StartedAt, "HH:mm")), ("end", session.EndedAt == null ? "-" : Time(session.EndedAt.Value, "HH:mm")))]);

        var ordered = entries
            .Where(e => e.Kind != AssemblyLogKind.SessionStarted && e.Kind != AssemblyLogKind.SessionEnded)
            .OrderBy(e => e.At)
            .ToList();
        var groups = ordered
            .GroupBy(e => e.ItemTitle ?? "")
            .OrderBy(g => g.Min(e => e.At));

        foreach (var group in groups) {
            builder.AppendLine();
            builder.AppendLine($"## {(group.Key.Length == 0 ? _i18n[I18nKey.Ui.AssemblyLog.NoItem] : group.Key)}");
            builder.AppendLine();
            foreach (var entry in group)
                builder.AppendLine($"- {Time(entry.At, "HH:mm:ss")} {MarkdownLine(entry)}");
        }

        return builder.ToString();
    }

    public static string FileName(string eventName, AssemblySession session, string extension) {
        var safe = new string(eventName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"protokollhilfe-{safe}-{BerlinTime.FromUtc(session.StartedAt):yyyy-MM-dd}.{extension}";
    }

    private string MarkdownLine(AssemblyLogEntry entry) {
        if (entry.Kind != AssemblyLogKind.Speech)
            return $"{_i18n[KindKey(entry.Kind)]}: {DetailText(entry)}";

        var duration = Duration(entry) ?? "-";
        var key = entry.AllottedSeconds == null ? I18nKey.Ui.AssemblyLog.SpeechLineUntimed : I18nKey.Ui.AssemblyLog.SpeechLine;
        return _i18n[I18nParams.With(key, ("role", RoleLabel(entry)), ("name", entry.MumbleName ?? ""), ("duration", duration),
            ("allotted", entry.AllottedSeconds == null ? "" : Minutes(entry.AllottedSeconds.Value)))];
    }

    // Poll entries store "title\nresult key"; every other kind stores an i18n key (or plain text) in Detail.
    private string DetailText(AssemblyLogEntry entry) {
        if (string.IsNullOrEmpty(entry.Detail))
            return "";
        if (entry.Kind != AssemblyLogKind.Poll)
            return _i18n[entry.Detail];

        var separator = entry.Detail.IndexOf('\n');
        if (separator < 0)
            return entry.Detail;

        var title = entry.Detail[..separator];
        var result = entry.Detail[(separator + 1)..];
        return result.Length == 0 ? title : $"{title}: {_i18n[result]}";
    }

    private string RoleLabel(AssemblyLogEntry entry) {
        return Enum.TryParse<SpeakerRole>(entry.Role, out var role) ? _i18n[AssemblyLabels.RoleKey(role)] : entry.Role ?? "";
    }

    private static string? Duration(AssemblyLogEntry entry) {
        if (entry.StartedAt == null || entry.EndedAt == null)
            return null;

        return Minutes((int)Math.Round((entry.EndedAt.Value - entry.StartedAt.Value).TotalSeconds));
    }

    private static string Minutes(int seconds) {
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static string Time(DateTime utc, string format) {
        return BerlinTime.FromUtc(utc).ToString(format);
    }

    private static string KindKey(AssemblyLogKind kind) {
        return kind switch {
            AssemblyLogKind.SessionStarted => I18nKey.Ui.AssemblyLog.KindSessionStarted,
            AssemblyLogKind.SessionEnded => I18nKey.Ui.AssemblyLog.KindSessionEnded,
            AssemblyLogKind.Speech => I18nKey.Ui.AssemblyLog.KindSpeech,
            AssemblyLogKind.PhaseChange => I18nKey.Ui.AssemblyLog.KindPhaseChange,
            AssemblyLogKind.Poll => I18nKey.Ui.AssemblyLog.KindPoll,
            AssemblyLogKind.ItemChange => I18nKey.Ui.AssemblyLog.KindItemChange,
            _ => I18nKey.Ui.AssemblyLog.KindChairAction
        };
    }

    private void AppendRow(StringBuilder builder, IReadOnlyList<string> values, bool translate) {
        builder.AppendLine(string.Join(";", values.Select(v => Escape(translate ? _i18n[v] : v))));
    }

    // A leading =, +, -, @ or tab would make spreadsheet programs evaluate a Mumble name or title as a formula.
    private static string Escape(string value) {
        if (value.Length > 0 && "=+-@\t".Contains(value[0]))
            value = "'" + value;
        if (value.IndexOfAny([';', '"', '\n', '\r']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
