using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Http;

/// <summary>
/// Parses error responses from the Quartermaster API (FastEndpoints / ProblemDetails
/// format) and translates error codes to user-facing strings via <see cref="I18nService"/>.
///
/// The API error shape is:
/// <code>
/// {
///   "status": 400,
///   "title": "Bad Request",
///   "errors": [
///     { "name": "fieldName", "reason": "error.code.key" }
///   ]
/// }
/// </code>
///
/// Each <c>reason</c> is an i18n key (possibly with query-string-encoded parameters)
/// that gets run through <see cref="I18nService.Translate"/>.
/// </summary>
public static class ApiErrorHelper {
    /// <summary>
    /// Reads the response body, parses the error array, and returns a list of
    /// translated German strings ready to display. Returns an empty list if the
    /// response was successful or the body didn't parse.
    /// </summary>
    public static async Task<List<string>> GetTranslatedErrorsAsync(
        HttpResponseMessage response, I18nService i18n) {
        var result = new List<string>();
        if (response.IsSuccessStatusCode)
            return result;

        try {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
                return result;

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errors", out var errors))
                return result;
            if (errors.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var error in errors.EnumerateArray()) {
                if (error.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String) {
                    var code = reason.GetString() ?? "";
                    result.Add(i18n.Translate(code));
                }
            }
        } catch (JsonException) {
            // Body wasn't JSON — fall through with empty list.
        }

        return result;
    }

    /// <summary>
    /// Convenience overload that joins all translated errors into a single
    /// newline-separated string. Returns null if no errors were found.
    /// </summary>
    public static async Task<string?> GetCombinedErrorMessageAsync(
        HttpResponseMessage response, I18nService i18n) {
        var errors = await GetTranslatedErrorsAsync(response, i18n);
        return errors.Count == 0 ? null : string.Join("\n", errors);
    }
}
