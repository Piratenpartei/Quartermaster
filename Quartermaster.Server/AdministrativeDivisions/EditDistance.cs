using System;

namespace Quartermaster.Server.AdministrativeDivisions;

public static class EditDistance {
    /// <summary>
    /// Optimal string alignment distance (insert, delete, replace, swap two neighbours), or <paramref name="max"/> + 1 as soon as it
    /// is certain to exceed <paramref name="max"/>.
    /// </summary>
    public static int Bounded(ReadOnlySpan<char> a, ReadOnlySpan<char> b, int max) {
        if (Math.Abs(a.Length - b.Length) > max)
            return max + 1;

        Span<int> previousPrevious = stackalloc int[b.Length + 1];
        Span<int> previous = stackalloc int[b.Length + 1];
        Span<int> current = stackalloc int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++) {
            current[0] = i;
            var rowMinimum = i;
            for (var j = 1; j <= b.Length; j++) {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    value = Math.Min(value, previousPrevious[j - 2] + 1);

                current[j] = value;
                rowMinimum = Math.Min(rowMinimum, value);
            }

            if (rowMinimum > max)
                return max + 1;

            var recycled = previousPrevious;
            previousPrevious = previous;
            previous = current;
            current = recycled;
        }

        return Math.Min(previous[b.Length], max + 1);
    }
}
