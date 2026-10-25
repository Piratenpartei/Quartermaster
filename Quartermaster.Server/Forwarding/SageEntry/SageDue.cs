using System;
using System.Globalization;

namespace Quartermaster.Server.Forwarding.SageEntry;

public static class SageDue {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>The due for the entry year: one twelfth of the yearly amount per month from the entry month on.</summary>
    public static decimal Partial(decimal yearly, DateOnly entryDate) {
        var months = 13 - entryDate.Month;
        return Math.Round(yearly * months / 12m, 2, MidpointRounding.AwayFromZero);
    }

    public static string MonthName(int month) {
        return German.DateTimeFormat.GetMonthName(month);
    }
}
