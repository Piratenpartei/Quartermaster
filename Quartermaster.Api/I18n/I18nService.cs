using System.Collections.Generic;
using System.Text.Json;
using System.Web;

namespace Quartermaster.Api.I18n;

/// <summary>
/// Generic internationalization service. Wraps a flat key→value translation
/// dictionary and provides lookup with placeholder substitution.
///
/// The service is intentionally load-agnostic: each project (server, client)
/// loads the locale JSON in whatever way is most natural for it (file system,
/// HTTP, etc.) and passes the JSON content to the constructor. This keeps a
/// single source of truth for the translation files (currently
/// <c>Quartermaster.Server/wwwroot/i18n/&lt;locale&gt;.json</c>) without
/// embedding them into any assembly.
///
/// Translation keys live in <see cref="I18nKey"/> as const strings. Templates
/// support <c>{name}</c> placeholders that get substituted from query-string
/// parameters appended to the key (see <see cref="I18nParams"/>).
/// </summary>
public class I18nService {
    private readonly Dictionary<string, string> _translations;

    /// <summary>
    /// Constructs an i18n service from a JSON document containing a flat
    /// dictionary of <c>{ "error.x.y": "translation", ... }</c>. Pass an empty
    /// string for an empty service that always falls back to raw keys.
    /// </summary>
    public I18nService(string jsonContent) {
        if (string.IsNullOrWhiteSpace(jsonContent)) {
            _translations = new Dictionary<string, string>();
            return;
        }
        _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent)
            ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Translates a key (optionally with query-string-encoded parameters) into
    /// the user-facing string. Placeholders in the template use {name} syntax.
    ///
    /// Examples:
    /// - <c>Translate("error.motion.title_required")</c>
    /// - <c>Translate("error.meeting.status.transition_invalid?from=Draft&amp;to=Completed")</c>
    ///
    /// If the key is not found, returns the raw key so missing translations are
    /// visible during development.
    /// </summary>
    public string Translate(string keyWithParams) {
        if (string.IsNullOrEmpty(keyWithParams))
            return "";

        var (key, parameters) = ParseKey(keyWithParams);

        if (!_translations.TryGetValue(key, out var template))
            return keyWithParams;

        if (parameters.Count == 0)
            return template;

        return Substitute(template, parameters);
    }

    /// <summary>
    /// Splits a key-with-params string on the first '?' into a bare key and a
    /// parsed query-string parameter dictionary.
    /// </summary>
    private static (string Key, Dictionary<string, string> Parameters) ParseKey(string input) {
        var queryIdx = input.IndexOf('?');
        if (queryIdx < 0)
            return (input, new Dictionary<string, string>());

        var key = input.Substring(0, queryIdx);
        var query = input.Substring(queryIdx + 1);
        var parameters = new Dictionary<string, string>();

        foreach (var pair in query.Split('&', System.StringSplitOptions.RemoveEmptyEntries)) {
            var eq = pair.IndexOf('=');
            if (eq < 0) {
                parameters[HttpUtility.UrlDecode(pair)] = "";
            } else {
                var k = HttpUtility.UrlDecode(pair.Substring(0, eq));
                var v = HttpUtility.UrlDecode(pair.Substring(eq + 1));
                parameters[k] = v;
            }
        }

        return (key, parameters);
    }

    /// <summary>
    /// Substitutes <c>{name}</c> placeholders in the template with values from
    /// the parameter dictionary. Unknown placeholders are left as-is so that
    /// typos become visible in the rendered output.
    /// </summary>
    private static string Substitute(string template, Dictionary<string, string> parameters) {
        foreach (var (key, value) in parameters)
            template = template.Replace("{" + key + "}", value);
        return template;
    }
}
