using System;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static class FeedUrl {
    /// <summary>A feed is untrusted input; only an absolute http(s) URL may reach the review page, so a feed
    /// cannot smuggle a javascript: or other non-http link into a reviewer's browser.</summary>
    public static bool IsHttp(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
