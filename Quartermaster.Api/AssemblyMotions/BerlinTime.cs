using System;

namespace Quartermaster.Api.AssemblyMotions;

public static class BerlinTime {
    private static readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static DateTime ToUtc(DateTime berlinLocal) {
        var unspecified = DateTime.SpecifyKind(berlinLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _zone);
    }

    public static DateTime FromUtc(DateTime utc) {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var converted = TimeZoneInfo.ConvertTimeFromUtc(asUtc, _zone);
        return DateTime.SpecifyKind(converted, DateTimeKind.Unspecified);
    }

    public static string FormatDeadline(DateTime utc) {
        var berlin = FromUtc(utc);
        return berlin.ToString("dd.MM. 'um' HH:mm 'Uhr'");
    }
}
