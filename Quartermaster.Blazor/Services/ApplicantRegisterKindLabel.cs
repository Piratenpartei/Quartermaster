using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Services;

public static class ApplicantRegisterKindLabel {
    public static string Of(I18nService i18n, ApplicantRegisterKind kind) {
        return kind switch {
            ApplicantRegisterKind.Rejected => i18n[I18nKey.Ui.ApplicantRegister.KindRejected],
            ApplicantRegisterKind.Expelled => i18n[I18nKey.Ui.ApplicantRegister.KindExpelled],
            ApplicantRegisterKind.LeftDuringExpulsion => i18n[I18nKey.Ui.ApplicantRegister.KindLeftDuringExpulsion],
            _ => kind.ToString()
        };
    }
}
