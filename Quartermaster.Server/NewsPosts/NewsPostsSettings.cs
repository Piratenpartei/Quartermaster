using System;
using System.Globalization;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.NewsPosts;

public class NewsPostsSettings {
    public bool Enabled { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaxItemAge { get; init; } = TimeSpan.FromHours(48);
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(30);
    public string MistralApiKey { get; init; } = "";
    public string MistralBaseUrl { get; init; } = "https://api.mistral.ai";
    public string TriageModel { get; init; } = "mistral-small-latest";
    public string GenerationModel { get; init; } = "mistral-medium-latest";
    public string EmbeddingModel { get; init; } = "mistral-embed";
    public double MinSimilarity { get; init; } = 0.70;
    public int CandidateCount { get; init; } = 5;
    public string GenerationPrompt { get; init; } = "";
    public string PostizBaseUrl { get; init; } = "";
    public string PostizApiKey { get; init; } = "";
    public string XReplySetting { get; init; } = "everyone";

    public bool HasMistral => !string.IsNullOrWhiteSpace(MistralApiKey);
    public bool HasPostiz => !string.IsNullOrWhiteSpace(PostizBaseUrl) && !string.IsNullOrWhiteSpace(PostizApiKey);

    public static NewsPostsSettings ReadFrom(OptionRepository options) {
        string Value(string id, string fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        double Number(string id, double fallback) {
            return double.TryParse(Value(id, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        return new NewsPostsSettings {
            Enabled = string.Equals(Value("newsposts.enabled", "false"), "true", StringComparison.OrdinalIgnoreCase),
            PollInterval = TimeSpan.FromMinutes(Math.Max(1, Number("newsposts.poll_interval_minutes", 15))),
            MaxItemAge = TimeSpan.FromHours(Math.Max(1, Number("newsposts.max_item_age_hours", 48))),
            Retention = TimeSpan.FromDays(Math.Max(1, Number("newsposts.retention_days", 30))),
            MistralApiKey = Value("newsposts.mistral.api_key", ""),
            MistralBaseUrl = Value("newsposts.mistral.base_url", "https://api.mistral.ai").TrimEnd('/'),
            TriageModel = Value("newsposts.mistral.triage_model", "mistral-small-latest"),
            GenerationModel = Value("newsposts.mistral.generation_model", "mistral-medium-latest"),
            EmbeddingModel = Value("newsposts.mistral.embedding_model", "mistral-embed"),
            MinSimilarity = Number("newsposts.triage.min_similarity", 0.70),
            CandidateCount = (int)Math.Clamp(Number("newsposts.generation.candidate_count", 5), 1, 10),
            GenerationPrompt = Value("newsposts.generation.prompt", ""),
            PostizBaseUrl = Value("newsposts.postiz.base_url", "").TrimEnd('/'),
            PostizApiKey = Value("newsposts.postiz.api_key", ""),
            XReplySetting = Value("newsposts.postiz.x_reply_setting", "everyone")
        };
    }
}
