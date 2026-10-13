using System.Collections.Generic;

namespace Quartermaster.Rendering;

/// <summary>
/// Lexical limits that are checked before Markdig sees a motion text. Every limit is an upper bound that holds whatever Markdig makes of the text.
/// A paragraph here is a run of lines without a blank line; no Markdig block that holds inline content or a table spans more than one of them.
/// </summary>
internal static class MotionMarkdownLimits {
    // Markdig pads every row of a pipe table to the widest row, so a table costs rows x columns cells although the text only grows with rows + columns
    // (12,500 x 25,000 fit into 100,000 characters). 10,000 cells render and sanitize in under 0.2 s; a 20 x 250 table, far beyond any real motion, counts 5,544.
    public const int MaxTableCells = 10_000;

    // Each failed inline link attempt ("[a](b (") makes Markdig scan to the end of the paragraph, about 0.6 ms per bracket in a 100,000 character paragraph.
    // 250 brackets keep the worst case near 150 ms and are more links than one paragraph, list or table of a motion holds.
    public const int MaxBracketsPerParagraph = 250;

    // Markdig matches emphasis delimiters in quadratic time per paragraph (10,000 runs in one paragraph: 650 ms, 1,000: 8 ms), and the sanitizer
    // serializes the children of one element in quadratic time (25,000 code spans in one paragraph: 1.7 s). Runs of *, _, ~, ` and < are what
    // these are made of. The total bounds the sum over all paragraphs (at most 10 full ones, about 80 ms).
    public const int MaxInlineMarkersPerParagraph = 1_000;
    public const int MaxInlineMarkers = 10_000;

    // Headings, list items, rules and line breaks cost one or more sibling elements per line, which the sanitizer serializes in quadratic time
    // (50,000 one-character headings: 6.7 s, 5,000: 0.1 s). A text of 100,000 characters needs lines below 20 characters on average to get here.
    public const int MaxLines = 5_000;

    // Markdig refuses more than 128 nested blocks, but only when it renders: a single line of 30,000 quote or list markers first builds
    // all of them, which takes 0.6 s. Deeper nesting than this per line has no use, so it is reported without parsing.
    public const int MaxContainerMarkersPerLine = 100;

    public static void Find(string markdown, HashSet<MotionMarkdownProblem> problems) {
        var paragraph = new Paragraph();
        long tableCells = 0;
        var inlineMarkers = 0;
        var lines = 0;

        var lineStart = 0;
        while (lineStart <= markdown.Length) {
            var lineEnd = markdown.IndexOf('\n', lineStart);
            if (lineEnd < 0)
                lineEnd = markdown.Length;

            if (LeadingContainerMarkers(markdown, lineStart, lineEnd) > MaxContainerMarkersPerLine)
                problems.Add(MotionMarkdownProblem.TooDeeplyNested);

            var isBlank = paragraph.AddLine(markdown, lineStart, lineEnd);
            if (isBlank || lineEnd == markdown.Length) {
                tableCells += paragraph.TableCells;
                inlineMarkers += paragraph.InlineMarkers;
                if (paragraph.Brackets > MaxBracketsPerParagraph)
                    problems.Add(MotionMarkdownProblem.TooManyBrackets);
                if (paragraph.InlineMarkers > MaxInlineMarkersPerParagraph)
                    problems.Add(MotionMarkdownProblem.TooMuchInlineMarkup);

                paragraph = new Paragraph();
            }

            lineStart = lineEnd + 1;
            lines++;
        }

        if (lines > MaxLines)
            problems.Add(MotionMarkdownProblem.TooManyLines);
        if (tableCells > MaxTableCells)
            problems.Add(MotionMarkdownProblem.TableTooLarge);
        if (inlineMarkers > MaxInlineMarkers)
            problems.Add(MotionMarkdownProblem.TooMuchInlineMarkup);
    }

    private static int LeadingContainerMarkers(string markdown, int start, int end) {
        var markers = 0;
        var i = start;
        while (i < end) {
            var c = markdown[i];
            if (c == ' ' || c == '\t') {
                i++;
                continue;
            }

            var markerEnd = c == '>' ? i + 1 : ListMarkerEnd(markdown, i, end);
            if (markerEnd < 0)
                break;

            markers++;
            i = markerEnd;
        }

        return markers;
    }

    /// <summary>
    /// Returns the index behind the bullet or ordered list marker that starts at the given index, or -1 when there is none.
    /// </summary>
    private static int ListMarkerEnd(string markdown, int start, int end) {
        var c = markdown[start];
        var next = start + 1;
        if (c != '-' && c != '+' && c != '*') {
            if (!char.IsAsciiDigit(c))
                return -1;

            while (next < end && char.IsAsciiDigit(markdown[next])) {
                next++;
            }
            if (next == end || (markdown[next] != '.' && markdown[next] != ')'))
                return -1;

            next++;
        }

        if (next < end && markdown[next] != ' ' && markdown[next] != '\t')
            return -1;

        return next;
    }

    private struct Paragraph {
        private int lines;
        private int widestLinePipes;

        public int Brackets { get; private set; }
        public int InlineMarkers { get; private set; }
        public readonly long TableCells => widestLinePipes == 0 ? 0 : (long)lines * (widestLinePipes + 1);

        /// <summary>
        /// Counts one line into the paragraph. Returns true for a blank line, which ends the paragraph and is not counted.
        /// </summary>
        public bool AddLine(string markdown, int start, int end) {
            var isBlank = true;
            var pipes = 0;

            for (var i = start; i < end; i++) {
                var c = markdown[i];
                if (c != ' ' && c != '\t')
                    isBlank = false;

                if (c == '|') {
                    pipes++;
                } else if (c == '[') {
                    Brackets++;
                } else if (IsInlineMarker(c) && (i == start || markdown[i - 1] != c)) {
                    InlineMarkers++;
                }
            }

            if (isBlank)
                return true;

            lines++;
            if (pipes > widestLinePipes)
                widestLinePipes = pipes;

            return false;
        }

        private static bool IsInlineMarker(char c) => c == '*' || c == '_' || c == '~' || c == '`' || c == '<';
    }
}
