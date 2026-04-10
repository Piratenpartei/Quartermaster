using System.Text;
using System.Web;

namespace Quartermaster.Api.I18n;

/// <summary>
/// Helper for building parameterized i18n keys. Encodes parameters as a
/// query string appended to the key so the whole thing fits in a single
/// error <c>reason</c> field.
///
/// Example:
/// <code>
/// ThrowError(I18nParams.With(I18nKey.Error.Meeting.StatusTransitionNotAllowed,
///     ("from", meeting.Status.ToString()),
///     ("to", req.Status.ToString())));
/// // Produces: "error.meeting.status.transition_not_allowed?from=Draft&amp;to=Completed"
/// </code>
/// </summary>
public static class I18nParams {
    public static string With(string key, params (string Key, string Value)[] parameters) {
        if (parameters.Length == 0)
            return key;

        var sb = new StringBuilder(key);
        sb.Append('?');
        for (var i = 0; i < parameters.Length; i++) {
            if (i > 0)
                sb.Append('&');
            sb.Append(HttpUtility.UrlEncode(parameters[i].Key));
            sb.Append('=');
            sb.Append(HttpUtility.UrlEncode(parameters[i].Value));
        }
        return sb.ToString();
    }
}
