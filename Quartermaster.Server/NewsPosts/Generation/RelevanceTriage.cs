using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Generation;

public class RelevanceTriage {
    private const string SystemPrompt =
        "Du prüfst, ob eine Nachrichtenmeldung einen klaren Bezug zu den angegebenen Programmstellen der Piratenpartei hat, " +
        "sodass die Partei sich glaubwürdig dazu äußern kann. Antworte ausschließlich als JSON: " +
        "{\"relevant\": true|false, \"reason\": \"ein kurzer Satz auf Deutsch\"}.";

    private readonly IMistralClient _mistral;
    private readonly NewsPostsSettings _settings;

    public RelevanceTriage(IMistralClient mistral, NewsPostsSettings settings) {
        _mistral = mistral;
        _settings = settings;
    }

    public async Task<TriageResult> RunAsync(NewsItem item, IReadOnlyList<ProgrammeSection> passages, CancellationToken ct) {
        var user = new StringBuilder();
        user.AppendLine("Meldung: " + item.Title);
        user.AppendLine("Teaser: " + item.Teaser);
        user.AppendLine();
        user.AppendLine("Programmstellen:");
        foreach (var passage in passages)
            user.AppendLine($"- {passage.HeadingPath}: {Shorten(passage.Body, 600)}");

        var raw = await _mistral.CompleteJsonAsync(_settings.TriageModel, SystemPrompt, user.ToString(), ct);
        using var doc = LlmJson.Parse(raw);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new MistralException("Triage answer is not a JSON object");
        if (!doc.RootElement.TryGetProperty("relevant", out var relevant) || !IsBoolean(relevant))
            throw new MistralException("Triage answer lacks a boolean 'relevant'");

        var reason = doc.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString() ?? ""
            : "";
        return new TriageResult(relevant.GetBoolean(), reason.Trim());
    }

    private static bool IsBoolean(JsonElement element) {
        return element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + "...";
}
