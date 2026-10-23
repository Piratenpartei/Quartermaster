using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <summary>
/// Searches every name of every division in memory, allowing typos and partly typed words. Words like "Landkreis" only count
/// when the search term contains them. The divisions only change through the import, which drops the index afterwards.
/// </summary>
public class AdministrativeDivisionSearchIndex {
    private const int PrefixCost = 1;
    private const int TypoCost = 2;
    private const int TypoPrefixCost = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdministrativeDivisionSearchIndex> _logger;
    private readonly Lock _lock = new();
    private IndexedDivision[]? _divisions;

    public AdministrativeDivisionSearchIndex(IServiceScopeFactory scopeFactory, ILogger<AdministrativeDivisionSearchIndex> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Invalidate() {
        using (_lock.EnterScope()) {
            _divisions = null;
        }
    }

    /// <summary>Builds the index now, so the first search after an import does not wait for it.</summary>
    public void Warm() {
        Divisions();
    }

    public static int MaxTypos(int length) {
        if (length < 4)
            return 0;

        return length < 8 ? 1 : 2;
    }

    public DivisionSearchResult Search(string query, int page, int pageSize) {
        var words = QueryWords(query);
        if (words.Count == 0)
            return new DivisionSearchResult([], 0);

        var coreWordCount = words.Count(w => !w.IsDesignator);
        var matches = new List<DivisionMatch>();
        foreach (var division in Divisions()) {
            var match = BestMatch(division, words, coreWordCount);
            if (match != null)
                matches.Add(match);
        }

        var items = matches
            .OrderBy(m => m.Cost)
            .ThenBy(m => m.ExtraWords)
            .ThenBy(m => m.MatchedName != null)
            .ThenBy(m => m.Division.Depth)
            .ThenBy(m => m.Division.Name, StringComparer.InvariantCulture)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new DivisionSearchHit(m.Division.Id, m.MatchedName))
            .ToList();
        return new DivisionSearchResult(items, matches.Count);
    }

    private static List<QueryWord> QueryWords(string query) {
        var words = DivisionNameTokenizer.Words(query);
        var lastIsPartial = query.Length > 0 && !char.IsWhiteSpace(query[^1]);
        return words
            .Select((w, i) => new QueryWord(w, LetterMask(w), DivisionNameTokenizer.IsDesignator(w), lastIsPartial && i == words.Count - 1))
            .ToList();
    }

    /// <summary>The cheapest of the division's names; the shown name comes first and wins ties.</summary>
    private static DivisionMatch? BestMatch(IndexedDivision division, List<QueryWord> words, int coreWordCount) {
        DivisionMatch? best = null;
        for (var i = 0; i < division.Names.Length; i++) {
            var name = division.Names[i];
            var cost = NameCost(name, words);
            if (cost == null)
                continue;

            var extra = Math.Max(0, name.Tokens.Core.Length - coreWordCount);
            if (!IsBetter(cost.Value, extra, best))
                continue;

            best = new DivisionMatch(division, cost.Value, extra, i == 0 ? null : name.Name);
        }

        return best;
    }

    private static bool IsBetter(int cost, int extra, DivisionMatch? best) {
        if (best == null)
            return true;
        if (cost != best.Cost)
            return cost < best.Cost;

        return extra < best.ExtraWords;
    }

    private static int? NameCost(IndexedName name, List<QueryWord> words) {
        var total = 0;
        foreach (var word in words) {
            var cost = WordCost(name, word);
            if (cost == null)
                return null;

            total += cost.Value;
        }

        return total;
    }

    private static int? WordCost(IndexedName name, QueryWord word) {
        if (word.IsDesignator && HasDesignator(name.Tokens, word.Text))
            return 0;
        if (word.IsDesignator && !word.IsPartial)
            return null;

        return CoreCost(name, word);
    }

    /// <summary>"kreis" also finds "Landkreis" and "Kreisfreie Stadt", "stadt" also "Hansestadt".</summary>
    private static bool HasDesignator(TokenizedName tokens, string word) {
        return tokens.Designators.Any(d => d.Contains(word, StringComparison.Ordinal))
            || tokens.Core.Any(c => c == word);
    }

    private static int? CoreCost(IndexedName name, QueryWord word) {
        var core = name.Tokens.Core;
        int? best = null;
        for (var i = 0; i < core.Length; i++)
            best = Cheaper(best, TokenCost(core[i], name.CoreMasks[i], word));
        if (core.Length > 1)
            best = Cheaper(best, TokenCost(name.Tokens.Joined, name.JoinedMask, word));

        return best;
    }

    private static int? TokenCost(string token, ulong tokenMask, QueryWord word) {
        var text = word.Text;
        if (token == text)
            return 0;
        if (token.StartsWith(text, StringComparison.Ordinal))
            return PrefixCost;

        // Every typo brings in at most one letter the token lacks, so more missing letters than typos rule the token out cheaply.
        var maxTypos = MaxTypos(text.Length);
        if (maxTypos == 0 || BitOperations.PopCount(word.Mask & ~tokenMask) > maxTypos)
            return null;

        var distance = EditDistance.Bounded(text, token, maxTypos);
        if (distance <= maxTypos)
            return TypoCost + distance;
        if (!word.IsPartial || token.Length <= text.Length)
            return null;

        // One typo at most: two in the first letters of a long word already match too much ("Oberdracken..." for "Oberfranken").
        var prefixDistance = EditDistance.Bounded(text, token.AsSpan(0, text.Length), 1);
        return prefixDistance <= 1 ? TypoPrefixCost + prefixDistance : null;
    }

    private static int? Cheaper(int? a, int? b) {
        if (a == null)
            return b;
        if (b == null)
            return a;

        return Math.Min(a.Value, b.Value);
    }

    private IndexedDivision[] Divisions() {
        using (_lock.EnterScope()) {
            _divisions ??= Build();
            return _divisions;
        }
    }

    private IndexedDivision[] Build() {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        var divisions = context.AdministrativeDivisions
            .Select(d => new { d.Id, d.Name, d.Depth })
            .ToList();
        var namesByDivision = context.AdministrativeDivisionNames
            .Select(n => new { n.AdministrativeDivisionId, n.Name })
            .ToList()
            .GroupBy(n => n.AdministrativeDivisionId)
            .ToDictionary(g => g.Key, g => g.Select(n => n.Name).ToList());

        var indexed = divisions
            .Select(d => new IndexedDivision(d.Id, d.Name, d.Depth, IndexNames(d.Name, namesByDivision.GetValueOrDefault(d.Id))))
            .ToArray();
        _logger.LogInformation("Division search index built: {Divisions} divisions, {Names} names",
            indexed.Length, indexed.Sum(d => d.Names.Length));
        return indexed;
    }

    /// <summary>One bit per letter or digit the word contains; everything else shares the top bit.</summary>
    private static ulong LetterMask(string word) {
        var mask = 0UL;
        foreach (var c in word) {
            if (c >= 'a' && c <= 'z') {
                mask |= 1UL << (c - 'a');
            } else if (c >= '0' && c <= '9') {
                mask |= 1UL << (26 + c - '0');
            } else {
                mask |= 1UL << 63;
            }
        }

        return mask;
    }

    /// <summary>The shown name first, then every other name that differs from the ones before it in more than case or punctuation.</summary>
    private static IndexedName[] IndexNames(string shownName, List<string>? otherNames) {
        var seen = new HashSet<string>();
        var names = new List<IndexedName>();
        IEnumerable<string> all = otherNames == null ? [shownName] : otherNames.Prepend(shownName);
        foreach (var name in all) {
            var tokens = DivisionNameTokenizer.Tokenize(name);
            if (seen.Add(string.Join(' ', tokens.Designators.Concat(tokens.Core))))
                names.Add(new IndexedName(name, tokens, tokens.Core.Select(LetterMask).ToArray(), LetterMask(tokens.Joined)));
        }

        return names.ToArray();
    }

    private record QueryWord(string Text, ulong Mask, bool IsDesignator, bool IsPartial);

    private record IndexedName(string Name, TokenizedName Tokens, ulong[] CoreMasks, ulong JoinedMask);

    private record IndexedDivision(Guid Id, string Name, int Depth, IndexedName[] Names);

    private record DivisionMatch(IndexedDivision Division, int Cost, int ExtraWords, string? MatchedName);
}
