using System.Collections.Generic;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;

namespace Quartermaster.Server.Settings;

public static class SettingsCheckResults {
    public const string UnexpectedErrorDetail = "Unerwarteter Fehler, Details stehen im Serverprotokoll.";

    private const int MaxDetailLength = 300;

    public static SettingsCheckResultDTO Ok(string messageKey, List<string>? items = null) {
        return new SettingsCheckResultDTO {
            Success = true,
            MessageKey = messageKey,
            Items = items ?? []
        };
    }

    public static SettingsCheckResultDTO NotConfigured() {
        return new SettingsCheckResultDTO { MessageKey = I18nKey.Ui.SettingsCheck.NotConfigured };
    }

    public static SettingsCheckResultDTO Failed(string messageKey, string? detail = null) {
        return new SettingsCheckResultDTO {
            MessageKey = messageKey,
            Detail = detail == null || detail.Length <= MaxDetailLength ? detail : detail[..MaxDetailLength]
        };
    }
}
