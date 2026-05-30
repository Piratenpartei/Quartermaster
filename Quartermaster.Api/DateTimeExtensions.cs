using System;

namespace Quartermaster.Api;

public static class DateTimeExtensions {
    extension(DateTime utc) {
        /// <summary>UTC-anchored offset wrapping a storage <see cref="DateTime"/> (LinqToDB returns <see cref="DateTimeKind.Unspecified"/>, so we attach offset zero explicitly).</summary>
        public DateTimeOffset ToDtoUtc() => new(utc, TimeSpan.Zero);

        public DateOnly ToDtoDate() => DateOnly.FromDateTime(utc);
    }

    extension(DateTime? utc) {
        public DateTimeOffset? ToDtoUtc() => utc.HasValue ? new DateTimeOffset(utc.Value, TimeSpan.Zero) : null;
        public DateOnly? ToDtoDate() => utc.HasValue ? DateOnly.FromDateTime(utc.Value) : null;
    }

    extension(DateOnly value) {
        public DateTime ToStorage() => value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    extension(DateOnly? value) {
        public DateTime? ToStorage() => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
    }
}
