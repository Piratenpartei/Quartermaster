using System;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public record WikiBotSettings(string ApiUrl, string Username, string Password, string ProtectionLevel) {
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    /// <summary>True for https URLs, and for http only on localhost, 127.0.0.1 and [::1].</summary>
    public static bool IsSecureUrl(string apiUrl) {
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri)) {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps) {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttp) {
            return false;
        }

        return IsLoopbackHost(uri.Host);
    }

    private static bool IsLoopbackHost(string host) {
        if (host == "localhost") {
            return true;
        }

        if (host == "127.0.0.1") {
            return true;
        }

        return host == "[::1]";
    }

    public static WikiBotSettings ReadFrom(OptionRepository options) {
        string Value(string id, string fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        return new WikiBotSettings(
            Value("wiki.api_url", "https://wiki.piratenpartei.de/api.php"),
            Value("wiki.bot_username", ""),
            Value("wiki.bot_password", ""),
            Value("wiki.protection_level", "Antrag"));
    }

    // The generated record ToString would print the password.
    public override string ToString() => $"WikiBotSettings {{ ApiUrl = {ApiUrl}, Username = {Username}, ProtectionLevel = {ProtectionLevel} }}";
}
