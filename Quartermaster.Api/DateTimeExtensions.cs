using System;

namespace Quartermaster.Api;

public static class DateTimeExtensions {
    extension(DateTime utc) {
        /// <summary>
        /// Wraps a UTC-convention <see cref="DateTime"/> from storage as a UTC-anchored
        /// <see cref="DateTimeOffset"/> for DTO emission, regardless of the value's
        /// <see cref="DateTime.Kind"/> (LinqToDB returns <see cref="DateTimeKind.Unspecified"/>
        /// from MySQL reads).
        /// </summary>
        public DateTimeOffset ToDtoUtc() => new(utc, TimeSpan.Zero);

        /// <summary>Date portion of a storage <see cref="DateTime"/> as a calendar <see cref="DateOnly"/> for DTO emission.</summary>
        public DateOnly ToDtoDate() => DateOnly.FromDateTime(utc);
    }

    extension(DateTime? utc) {
        /// <summary>Nullable companion of <c>DateTime.ToDtoUtc()</c>.</summary>
        public DateTimeOffset? ToDtoUtc() => utc.HasValue ? new DateTimeOffset(utc.Value, TimeSpan.Zero) : null;

        /// <summary>Nullable companion of <c>DateTime.ToDtoDate()</c>.</summary>
        public DateOnly? ToDtoDate() => utc.HasValue ? DateOnly.FromDateTime(utc.Value) : null;
    }

    extension(DateOnly value) {
        /// <summary>Converts a wire <see cref="DateOnly"/> back to a midnight-UTC <see cref="DateTime"/> for storage.</summary>
        public DateTime ToStorage() => value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    extension(DateOnly? value) {
        /// <summary>Nullable companion of <c>DateOnly.ToStorage()</c>.</summary>
        public DateTime? ToStorage() => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
    }
}
