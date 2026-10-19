using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Forum;

public record DiscourseSettings(string BaseUrl, string ApiUsername, string ApiKey, int MaxPostLength) {
    public const int DefaultMaxPostLength = 32000;

    private const int MinMaxPostLength = 1000;

    public bool IsConfigured() {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            return false;
        if (string.IsNullOrWhiteSpace(ApiUsername))
            return false;
        return !string.IsNullOrWhiteSpace(ApiKey);
    }

    /// <summary>True for https URLs, and for http only on localhost, 127.0.0.1 and [::1].</summary>
    public static bool IsSecureUrl(string url) {
        return WikiBotSettings.IsSecureUrl(url);
    }

    public string TopicUrl(int topicId) {
        return $"{BaseUrl}/t/{topicId}";
    }

    public static DiscourseSettings ReadFrom(OptionRepository options) {
        string Value(string id) {
            return options.GetGlobalValue(id)?.Value?.Trim() ?? "";
        }

        return new DiscourseSettings(
            Value("discourse.base_url").TrimEnd('/'),
            Value("discourse.api_username"),
            Value("discourse.api_key"),
            ParseMaxPostLength(Value("discourse.max_post_length")));
    }

    private static int ParseMaxPostLength(string value) {
        if (!int.TryParse(value, out var length))
            return DefaultMaxPostLength;
        return length < MinMaxPostLength ? DefaultMaxPostLength : length;
    }

    // The generated record ToString would print the API key.
    public override string ToString() {
        return $"DiscourseSettings {{ BaseUrl = {BaseUrl}, ApiUsername = {ApiUsername}, MaxPostLength = {MaxPostLength} }}";
    }
}
