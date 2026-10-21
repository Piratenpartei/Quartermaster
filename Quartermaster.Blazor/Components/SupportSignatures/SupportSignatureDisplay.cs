using System.Collections.Generic;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public static class SupportSignatureDisplay {
    public static readonly IReadOnlyList<SignatureRejectionReason> Reasons = [
        SignatureRejectionReason.NoVotingRight,
        SignatureRejectionReason.NotResident,
        SignatureRejectionReason.IllegibleOrIncomplete,
        SignatureRejectionReason.SignedTwice,
        SignatureRejectionReason.Other
    ];

    public static string ReasonLabel(I18nService i18n, SignatureRejectionReason? reason) {
        return reason switch {
            SignatureRejectionReason.NoVotingRight => i18n[I18nKey.Ui.SupportSignatures.ReasonNoVotingRight],
            SignatureRejectionReason.NotResident => i18n[I18nKey.Ui.SupportSignatures.ReasonNotResident],
            SignatureRejectionReason.IllegibleOrIncomplete => i18n[I18nKey.Ui.SupportSignatures.ReasonIllegible],
            SignatureRejectionReason.SignedTwice => i18n[I18nKey.Ui.SupportSignatures.ReasonSignedTwice],
            SignatureRejectionReason.Other => i18n[I18nKey.Ui.SupportSignatures.ReasonOther],
            _ => i18n[I18nKey.Ui.SupportSignatures.ReasonNone]
        };
    }
}
