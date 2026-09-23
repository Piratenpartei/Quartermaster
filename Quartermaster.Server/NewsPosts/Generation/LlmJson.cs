using System.Text.Json;
using System.Text.RegularExpressions;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Generation;

public static partial class LlmJson {
    /// <summary>JSON mode normally returns bare JSON, but models occasionally wrap it in a Markdown fence; accept both.</summary>
    public static JsonDocument Parse(string raw) {
        var match = FenceRegex().Match(raw);
        var json = match.Success ? match.Groups[1].Value : raw;
        try {
            return JsonDocument.Parse(json);
        } catch (JsonException ex) {
            throw new MistralException("Model answer is not valid JSON: " + ex.Message);
        }
    }

    [GeneratedRegex(@"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex FenceRegex();
}
