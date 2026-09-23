using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Generation;

public class CandidateGenerator {
    private readonly IMistralClient _mistral;
    private readonly NewsPostsSettings _settings;

    public CandidateGenerator(IMistralClient mistral, NewsPostsSettings settings) {
        _mistral = mistral;
        _settings = settings;
    }

    public async Task<List<GeneratedCandidate>> GenerateAsync(NewsItem item, IReadOnlyList<ProgrammeSection> passages, CancellationToken ct) {
        var raw = await _mistral.CompleteJsonAsync(_settings.GenerationModel, BuildSystemPrompt(), BuildUserPrompt(item, passages), ct);
        using var doc = LlmJson.Parse(raw);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            throw new MistralException("Generation answer lacks a 'candidates' array");

        var parsed = candidates.EnumerateArray()
            .Select(ParseCandidate)
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
        return CandidateValidator.Validate(parsed, passages.Select(p => p.Id).ToList(), _settings.CandidateCount);
    }

    private string BuildSystemPrompt() {
        return _settings.GenerationPrompt + "\n\n" +
            $"Schreibe bis zu {_settings.CandidateCount} verschiedene Beiträge auf Deutsch zu der Meldung, jeder höchstens {PostText.MaxLength} Zeichen lang. " +
            "Zahlen, Namen und Zitate dürfen nur aus dem Artikeltext stammen, nichts erfinden. " +
            "Jeder Beitrag stützt sich auf genau eine der angegebenen Programmstellen und nennt deren Id. " +
            "Wenn keine Programmstelle wirklich passt, gib eine leere Liste zurück. " +
            "Antworte ausschließlich als JSON: {\"candidates\": [{\"text\": \"...\", \"sectionId\": \"<Id>\"}]}.";
    }

    private static string BuildUserPrompt(NewsItem item, IReadOnlyList<ProgrammeSection> passages) {
        var user = new StringBuilder();
        user.AppendLine("Meldung: " + item.Title);
        user.AppendLine("Quelle: " + item.Url);
        user.AppendLine();
        user.AppendLine("Artikeltext:");
        user.AppendLine(string.IsNullOrWhiteSpace(item.ArticleText) ? item.Teaser : item.ArticleText);
        user.AppendLine();
        user.AppendLine("Programmstellen:");
        foreach (var passage in passages) {
            user.AppendLine($"[Id {passage.Id}] {passage.HeadingPath}");
            user.AppendLine(passage.Body);
            user.AppendLine();
        }
        return user.ToString();
    }

    private static GeneratedCandidate? ParseCandidate(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        if (!element.TryGetProperty("text", out var t) || t.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("sectionId", out var s) || s.ValueKind != JsonValueKind.String)
            return null;
        if (!Guid.TryParse(s.GetString(), out var sectionId))
            return null;
        return new GeneratedCandidate(t.GetString()!, sectionId);
    }
}
