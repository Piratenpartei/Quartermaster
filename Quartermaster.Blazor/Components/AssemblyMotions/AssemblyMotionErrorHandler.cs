using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Http;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public static class AssemblyMotionErrorHandler {
    /// <summary>
    /// Toasts the translated server errors. Returns the conflict message instead of toasting when the response is a revision conflict, otherwise null.
    /// </summary>
    public static async Task<string?> ShowAsync(HttpResponseMessage response, I18nService i18n, ToastService toastService) {
        var messages = await ApiErrorHelper.GetTranslatedErrorsAsync(response, i18n);
        var revisionConflict = i18n[I18nKey.Error.AssemblyMotion.RevisionConflict];
        if (response.StatusCode == HttpStatusCode.Conflict && messages.Contains(revisionConflict))
            return revisionConflict;

        if (messages.Count > 0) {
            toastService.Error(string.Join("\n", messages));
        } else {
            toastService.ErrorKey(I18nKey.Ui.AssemblyMotion.SaveFailed);
        }
        return null;
    }
}
