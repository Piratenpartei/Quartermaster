using System;
using System.Globalization;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public static class AssemblyMotionDisplay {
    private const string InputFormat = "yyyy-MM-ddTHH:mm";

    private static readonly string[] _inputParseFormats = [
        InputFormat,
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF"
    ];

    public static string StatusLabel(I18nService i18n, AssemblyMotionStatus status, bool isInRevision) {
        if (isInRevision)
            return i18n[I18nKey.Ui.AssemblyMotion.StatusInRevision];

        return status switch {
            AssemblyMotionStatus.Draft => i18n[I18nKey.Ui.AssemblyMotion.StatusDraft],
            AssemblyMotionStatus.CollectingCoSubmitters => i18n[I18nKey.Ui.AssemblyMotion.StatusCollecting],
            AssemblyMotionStatus.Submitted => i18n[I18nKey.Ui.AssemblyMotion.StatusSubmitted],
            AssemblyMotionStatus.Admitted => i18n[I18nKey.Ui.AssemblyMotion.StatusAdmitted],
            AssemblyMotionStatus.FormallyInsufficient => i18n[I18nKey.Ui.AssemblyMotion.StatusFormallyInsufficient],
            AssemblyMotionStatus.Withdrawn => i18n[I18nKey.Ui.AssemblyMotion.StatusWithdrawn],
            AssemblyMotionStatus.Lapsed => i18n[I18nKey.Ui.AssemblyMotion.StatusLapsed],
            _ => status.ToString()
        };
    }

    public static string ResultLabel(I18nService i18n, AssemblyMotionResult result) => result switch {
        AssemblyMotionResult.Accepted => i18n[I18nKey.Ui.AssemblyMotion.ResultAccepted],
        AssemblyMotionResult.AcceptedModified => i18n[I18nKey.Ui.AssemblyMotion.ResultAcceptedModified],
        AssemblyMotionResult.Rejected => i18n[I18nKey.Ui.AssemblyMotion.ResultRejected],
        AssemblyMotionResult.NotHandled => i18n[I18nKey.Ui.AssemblyMotion.ResultNotHandled],
        _ => ""
    };

    public static string MajorityLabel(I18nService i18n, RequiredMajority majority) => majority switch {
        RequiredMajority.TwoThirds => i18n[I18nKey.Ui.AssemblyMotion.MajorityTwoThirds],
        _ => i18n[I18nKey.Ui.AssemblyMotion.MajoritySimple]
    };

    public static string ProblemLabel(I18nService i18n, EligibilityProblem problem) => problem switch {
        EligibilityProblem.NotSignedIn => i18n[I18nKey.Ui.AssemblyMotion.ProblemNotSignedIn],
        EligibilityProblem.NoLinkedMember => i18n[I18nKey.Ui.AssemblyMotion.ProblemNoLinkedMember],
        EligibilityProblem.NoMemberNumber => i18n[I18nKey.Ui.AssemblyMotion.ProblemNoMemberNumber],
        EligibilityProblem.Pending => i18n[I18nKey.Ui.AssemblyMotion.ProblemPending],
        EligibilityProblem.Anonymized => i18n[I18nKey.Ui.AssemblyMotion.ProblemAnonymized],
        EligibilityProblem.Exited => i18n[I18nKey.Ui.AssemblyMotion.ProblemExited],
        EligibilityProblem.OutsideChapter => i18n[I18nKey.Ui.AssemblyMotion.ProblemOutsideChapter],
        _ => ""
    };

    public static string WikiSyncLabel(I18nService i18n, WikiSyncState state) => state switch {
        WikiSyncState.Pending => i18n[I18nKey.Ui.AssemblyMotion.WikiStatePending],
        WikiSyncState.Synced => i18n[I18nKey.Ui.AssemblyMotion.WikiStateSynced],
        WikiSyncState.Failed => i18n[I18nKey.Ui.AssemblyMotion.WikiStateFailed],
        WikiSyncState.ProtectionFailed => i18n[I18nKey.Ui.AssemblyMotion.WikiStateProtectionFailed],
        _ => i18n[I18nKey.Ui.AssemblyMotion.WikiStateNotMirrored]
    };

    public static string WikiSyncBadgeClass(WikiSyncState state) => state switch {
        WikiSyncState.Pending => "border-info text-info-emphasis",
        WikiSyncState.Synced => "border-success text-success-emphasis",
        WikiSyncState.Failed => "border-danger text-danger-emphasis",
        WikiSyncState.ProtectionFailed => "border-warning text-warning-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };

    public static string Berlin(DateTimeOffset value)
        => BerlinTime.FromUtc(value.UtcDateTime).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string ToInput(DateTimeOffset? value)
        => value == null ? "" : BerlinTime.FromUtc(value.Value.UtcDateTime).ToString(InputFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset? FromInput(string? value) {
        if (!DateTime.TryParseExact(value, _inputParseFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return null;

        return new DateTimeOffset(BerlinTime.ToUtc(parsed), TimeSpan.Zero);
    }
}
