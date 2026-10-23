using System;

namespace Quartermaster.Server.Events;

public static class EventMeetingUrl {
    /// <summary>Empty, or an absolute https link; the link is shown publicly as a button, so no other schemes.</summary>
    public static bool IsValid(string? url) {
        if (string.IsNullOrWhiteSpace(url))
            return true;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);
    }
}
