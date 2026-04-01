using Ganss.Xss;

namespace Quartermaster.Api.Rendering;

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

    private static readonly HtmlSanitizer StrictSanitizer = CreateStrictSanitizer();
    private static readonly HtmlSanitizer StandardSanitizer = CreateStandardSanitizer();

    public static string Sanitize(string html, SanitizationProfile profile) {
        if (string.IsNullOrEmpty(html))
            return html;

        var sanitizer = profile switch {
            SanitizationProfile.Strict => StrictSanitizer,
            SanitizationProfile.Standard => StandardSanitizer,
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
}
