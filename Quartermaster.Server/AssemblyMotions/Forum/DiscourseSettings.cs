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

    /// <inheritdoc cref="WikiBotSettings.IsSecureUrl"/>
    public static bool IsSecureUrl(string url, bool allowLoopbackHttp) {
        return WikiBotSettings.IsSecureUrl(url, allowLoopbackHttp);
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
