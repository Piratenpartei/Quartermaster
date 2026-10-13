using System;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public record OpenSlidesSettings(string Url, string Username, string Password) {
    public bool IsConfigured() {
        if (string.IsNullOrWhiteSpace(Url))
            return false;
        if (string.IsNullOrWhiteSpace(Username))
            return false;
        return !string.IsNullOrWhiteSpace(Password);
    }

    /// <summary>True for https URLs, and for http only on localhost, 127.0.0.1 and [::1].</summary>
    public static bool IsSecureUrl(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;
        if (uri.Scheme != Uri.UriSchemeHttp)
            return false;

        return IsLoopbackHost(uri.Host);
    }

    private static bool IsLoopbackHost(string host) {
        if (host == "localhost")
            return true;
        if (host == "127.0.0.1")
            return true;

        return host == "[::1]";
    }

    public static OpenSlidesSettings ReadFrom(OptionRepository options) {
        string Value(string id) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        return new OpenSlidesSettings(
            Value("openslides.url"),
            Value("openslides.username"),
            Value("openslides.password"));
    }

    // The generated record ToString would print the password.
    public override string ToString() {
        return $"OpenSlidesSettings {{ Url = {Url}, Username = {Username} }}";
    }
}
