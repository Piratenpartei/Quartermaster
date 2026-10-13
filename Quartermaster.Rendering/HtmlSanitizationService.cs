using System;
using System.Linq;
using Ganss.Xss;

namespace Quartermaster.Rendering;

public static class HtmlSanitizationService {
    private static readonly string[] FormattingTags = [
        "p", "br", "b", "i", "em", "strong", "u", "s", "del",
        "sup", "sub", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "pre", "code", "hr"
    ];

    private static readonly string[] RichContentTags = [
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "img"
    ];

    private static readonly string[] RichContentAttributes = [
        "href", "rel", "target", "src", "alt", "width", "height", "colspan", "rowspan"
    ];

    private static readonly string[] AllowedUriSchemes = ["https", "http", "mailto"];

    private static readonly string[] MotionTags = [
        "p", "br", "em", "strong", "del", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "pre", "code", "hr", "a", "table", "thead", "tbody", "tr", "th", "td"
    ];

    private static readonly string[] MotionLinkPrefixes = ["https://", "http://", "mailto:"];

    private static readonly HtmlSanitizer StrictSanitizer = CreateStrictSanitizer();
    private static readonly HtmlSanitizer StandardSanitizer = CreateStandardSanitizer();
    private static readonly HtmlSanitizer MotionSanitizer = CreateMotionSanitizer();

    public static string Sanitize(string html, SanitizationProfile profile) {
        if (string.IsNullOrEmpty(html))
            return html;

        var sanitizer = profile switch {
            SanitizationProfile.Strict => StrictSanitizer,
            SanitizationProfile.Standard => StandardSanitizer,
            SanitizationProfile.Motion => MotionSanitizer,
            _ => StrictSanitizer
        };

        return sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer CreateStrictSanitizer() {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();

        foreach (var tag in FormattingTags) {
            sanitizer.AllowedTags.Add(tag);
        }
        sanitizer.AllowedTags.Add("a"); // allowed without href — preserves link text as plain text
        sanitizer.AllowedAttributes.Add("class");

        return sanitizer;
    }

    private static HtmlSanitizer CreateStandardSanitizer() {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();

        foreach (var tag in FormattingTags) {
            sanitizer.AllowedTags.Add(tag);
        }
        sanitizer.AllowedTags.Add("a");
        foreach (var tag in RichContentTags) {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Add("class");
        foreach (var attr in RichContentAttributes) {
            sanitizer.AllowedAttributes.Add(attr);
        }
        foreach (var scheme in AllowedUriSchemes) {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }

    private static HtmlSanitizer CreateMotionSanitizer() {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();

        foreach (var tag in MotionTags) {
            sanitizer.AllowedTags.Add(tag);
        }
        sanitizer.AllowedAttributes.Add("href");
        foreach (var scheme in AllowedUriSchemes) {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        sanitizer.FilterUrl += FilterMotionUrl;

        return sanitizer;
    }

    // The scheme list alone lets relative and protocol-relative URLs through, and the attribute list is not per tag.
    private static void FilterMotionUrl(object? sender, FilterUrlEventArgs e) {
        if (e.Tag.LocalName != "a" || !IsMotionLink(e.SanitizedUrl))
            e.SanitizedUrl = null;
    }

    private static bool IsMotionLink(string? url)
        => url != null && MotionLinkPrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
