using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public enum TextDiffKind {
    Unchanged = 0,
    Added = 1,
    Removed = 2
}

public record TextDiffLine(TextDiffKind Kind, string Text);

public static class TextDiff {
    private const long MaxLcsCells = 4_000_000;

    public static List<TextDiffLine> Lines(string oldText, string newText) {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        if ((long)oldLines.Length * newLines.Length > MaxLcsCells)
            return Replace(oldLines, newLines);

        var lcs = BuildLcsTable(oldLines, newLines);
        var result = new List<TextDiffLine>();
        var oldIndex = 0;
        var newIndex = 0;

        while (oldIndex < oldLines.Length && newIndex < newLines.Length) {
            if (oldLines[oldIndex] == newLines[newIndex]) {
                result.Add(new TextDiffLine(TextDiffKind.Unchanged, oldLines[oldIndex]));
                oldIndex++;
                newIndex++;
            } else if (lcs[oldIndex + 1, newIndex] >= lcs[oldIndex, newIndex + 1]) {
                result.Add(new TextDiffLine(TextDiffKind.Removed, oldLines[oldIndex]));
                oldIndex++;
            } else {
                result.Add(new TextDiffLine(TextDiffKind.Added, newLines[newIndex]));
                newIndex++;
            }
        }

        for (; oldIndex < oldLines.Length; oldIndex++)
            result.Add(new TextDiffLine(TextDiffKind.Removed, oldLines[oldIndex]));
        for (; newIndex < newLines.Length; newIndex++)
            result.Add(new TextDiffLine(TextDiffKind.Added, newLines[newIndex]));

        return result;
    }

    private static string[] SplitLines(string text) {
        return text.Replace("\r\n", "\n").Split('\n');
    }

    private static int[,] BuildLcsTable(string[] oldLines, string[] newLines) {
        var table = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var i = oldLines.Length - 1; i >= 0; i--) {
            for (var j = newLines.Length - 1; j >= 0; j--) {
                table[i, j] = oldLines[i] == newLines[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }
        return table;
    }

    private static List<TextDiffLine> Replace(string[] oldLines, string[] newLines) {
        var result = new List<TextDiffLine>(oldLines.Length + newLines.Length);
        foreach (var line in oldLines)
            result.Add(new TextDiffLine(TextDiffKind.Removed, line));
        foreach (var line in newLines)
            result.Add(new TextDiffLine(TextDiffKind.Added, line));
        return result;
    }
}
