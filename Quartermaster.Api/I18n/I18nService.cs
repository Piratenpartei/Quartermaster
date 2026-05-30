using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Web;

namespace Quartermaster.Api.I18n;

/// <summary>
/// Key → user-facing string lookup. Keys may carry query-string parameters
/// (<c>"error.x?from=A&amp;to=B"</c>) that fill <c>{name}</c> placeholders in the
/// template. Missing keys return the raw key so untranslated strings are visible.
/// </summary>
public class I18nService {
    private Dictionary<string, string> _translations;

    public I18nService(string jsonContent) {
        _translations = Parse(jsonContent);
    }

    public void Reload(string jsonContent) {
        _translations = Parse(jsonContent);
    }

    private static Dictionary<string, string> Parse(string jsonContent) {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent)
            ?? new Dictionary<string, string>();
    }

    public string this[string keyWithParams] => Translate(keyWithParams);

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

    private static (string Key, Dictionary<string, string> Parameters) ParseKey(string input) {
        var queryIdx = input.IndexOf('?');
        if (queryIdx < 0)
            return (input, new Dictionary<string, string>());

        var key = input.Substring(0, queryIdx);
        var query = input.Substring(queryIdx + 1);
        var parameters = new Dictionary<string, string>();

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries)) {
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

    private static string Substitute(string template, Dictionary<string, string> parameters) {
        foreach (var (key, value) in parameters)
            template = template.Replace("{" + key + "}", value);
        return template;
    }
}
