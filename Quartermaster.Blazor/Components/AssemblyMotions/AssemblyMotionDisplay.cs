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

    public static string ResultLabel(I18nService i18n, AssemblyMotionResult result) {
        return result switch {
            AssemblyMotionResult.Accepted => i18n[I18nKey.Ui.AssemblyMotion.ResultAccepted],
            AssemblyMotionResult.AcceptedModified => i18n[I18nKey.Ui.AssemblyMotion.ResultAcceptedModified],
            AssemblyMotionResult.Rejected => i18n[I18nKey.Ui.AssemblyMotion.ResultRejected],
            AssemblyMotionResult.NotHandled => i18n[I18nKey.Ui.AssemblyMotion.ResultNotHandled],
            _ => ""
        };
    }

    public static string MajorityLabel(I18nService i18n, RequiredMajority majority) {
        return majority switch {
            RequiredMajority.TwoThirds => i18n[I18nKey.Ui.AssemblyMotion.MajorityTwoThirds],
            _ => i18n[I18nKey.Ui.AssemblyMotion.MajoritySimple]
        };
    }

    public static string ProblemLabel(I18nService i18n, EligibilityProblem problem) {
        return problem switch {
            EligibilityProblem.NotSignedIn => i18n[I18nKey.Ui.AssemblyMotion.ProblemNotSignedIn],
            EligibilityProblem.NoLinkedMember => i18n[I18nKey.Ui.AssemblyMotion.ProblemNoLinkedMember],
            EligibilityProblem.NoMemberNumber => i18n[I18nKey.Ui.AssemblyMotion.ProblemNoMemberNumber],
            EligibilityProblem.Pending => i18n[I18nKey.Ui.AssemblyMotion.ProblemPending],
            EligibilityProblem.Anonymized => i18n[I18nKey.Ui.AssemblyMotion.ProblemAnonymized],
            EligibilityProblem.Exited => i18n[I18nKey.Ui.AssemblyMotion.ProblemExited],
            EligibilityProblem.OutsideChapter => i18n[I18nKey.Ui.AssemblyMotion.ProblemOutsideChapter],
            _ => ""
        };
    }

    public static string WikiSyncLabel(I18nService i18n, WikiSyncState state) {
        return state switch {
            WikiSyncState.Pending => i18n[I18nKey.Ui.AssemblyMotion.WikiStatePending],
            WikiSyncState.Synced => i18n[I18nKey.Ui.AssemblyMotion.WikiStateSynced],
            WikiSyncState.Failed => i18n[I18nKey.Ui.AssemblyMotion.WikiStateFailed],
            WikiSyncState.ProtectionFailed => i18n[I18nKey.Ui.AssemblyMotion.WikiStateProtectionFailed],
            _ => i18n[I18nKey.Ui.AssemblyMotion.WikiStateNotMirrored]
        };
    }

    public static string WikiSyncBadgeClass(WikiSyncState state) {
        return state switch {
            WikiSyncState.Pending => "border-info text-info-emphasis",
            WikiSyncState.Synced => "border-success text-success-emphasis",
            WikiSyncState.Failed => "border-danger text-danger-emphasis",
            WikiSyncState.ProtectionFailed => "border-warning text-warning-emphasis",
            _ => "border-secondary text-secondary-emphasis"
        };
    }

    public static string ForumSyncLabel(I18nService i18n, ForumSyncState state) {
        return state switch {
            ForumSyncState.Pending => i18n[I18nKey.Ui.AssemblyMotion.ForumStatePending],
            ForumSyncState.Synced => i18n[I18nKey.Ui.AssemblyMotion.ForumStateSynced],
            ForumSyncState.Failed => i18n[I18nKey.Ui.AssemblyMotion.ForumStateFailed],
            ForumSyncState.Deleted => i18n[I18nKey.Ui.AssemblyMotion.ForumStateDeleted],
            _ => i18n[I18nKey.Ui.AssemblyMotion.ForumStateNotMirrored]
        };
    }

    public static string ForumSyncBadgeClass(ForumSyncState state) {
        return state switch {
            ForumSyncState.Pending => "border-info text-info-emphasis",
            ForumSyncState.Synced => "border-success text-success-emphasis",
            ForumSyncState.Failed => "border-danger text-danger-emphasis",
            ForumSyncState.Deleted => "border-warning text-warning-emphasis",
            _ => "border-secondary text-secondary-emphasis"
        };
    }

    public static readonly AssemblyMotionResult[] AllResults = [
        AssemblyMotionResult.None,
        AssemblyMotionResult.Accepted,
        AssemblyMotionResult.AcceptedModified,
        AssemblyMotionResult.Rejected,
        AssemblyMotionResult.NotHandled
    ];

    public static string ResultOptionLabel(I18nService i18n, AssemblyMotionResult result) {
        if (result == AssemblyMotionResult.None)
            return i18n[I18nKey.Ui.AssemblyMotion.ResultNone];

        return ResultLabel(i18n, result);
    }

    public static bool IsEventReached(DateOnly? eventDate) {
        if (eventDate == null)
            return false;

        var today = DateOnly.FromDateTime(BerlinTime.FromUtc(DateTime.UtcNow));
        return today >= eventDate.Value;
    }

    public static string ResultsLockedText(I18nService i18n, DateOnly? eventDate) {
        if (eventDate == null)
            return i18n[I18nKey.Ui.AssemblyMotion.ResultsNoEventDate];

        return i18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.ResultsEventNotReached, ("date", eventDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)))];
    }

    public static bool IsPrioritizationOpen(AssemblyMotionPortalDTO portal) {
        if (!portal.PrioritizationEnabled || portal.PrioritizationStartsAt == null || portal.PrioritizationEndsAt == null)
            return false;

        var now = DateTimeOffset.UtcNow;
        return portal.PrioritizationStartsAt <= now && now <= portal.PrioritizationEndsAt;
    }

    public static bool IsRankingAvailable(AssemblyMotionPortalDTO portal) {
        if (!portal.PrioritizationEnabled || portal.PrioritizationEndsAt == null)
            return false;

        return DateTimeOffset.UtcNow > portal.PrioritizationEndsAt;
    }

    public static bool IsPortalOpen(AssemblyMotionPortalDTO portal) {
        var now = DateTimeOffset.UtcNow;
        return portal.OpensAt <= now && now <= portal.ClosesAt;
    }

    public static string PortalClosedText(I18nService i18n, AssemblyMotionPortalDTO portal) {
        if (DateTimeOffset.UtcNow < portal.OpensAt)
            return i18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.PortalOpensHint, ("date", Berlin(portal.OpensAt)))];

        return i18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.PortalClosedHint, ("date", Berlin(portal.ClosesAt)))];
    }

    public static string Berlin(DateTimeOffset value) {
        return BerlinTime.FromUtc(value.UtcDateTime).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// For a deadline at Berlin midnight, the last second that still counts (the previous day, 23:59:59); null otherwise.
    /// Deadlines are stored as the instant they end, so "28.11. 00:00" reads clearer with this next to it.
    /// </summary>
    public static string? LastSecondBefore(DateTimeOffset deadline) {
        var berlin = BerlinTime.FromUtc(deadline.UtcDateTime);
        if (berlin.TimeOfDay != TimeSpan.Zero)
            return null;

        return berlin.AddSeconds(-1).ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>A deadline for use inside a sentence, with the last valid second added for midnight deadlines.</summary>
    public static string Deadline(I18nService i18n, DateTimeOffset deadline) {
        var lastSecond = LastSecondBefore(deadline);
        if (lastSecond == null)
            return Berlin(deadline);

        return $"{Berlin(deadline)} ({i18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.DeadlineUpToIncluding, ("date", lastSecond))]})";
    }

    public static string ToInput(DateTimeOffset? value) {
        return value == null ? "" : BerlinTime.FromUtc(value.Value.UtcDateTime).ToString(InputFormat, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset? FromInput(string? value) {
        if (!DateTime.TryParseExact(value, _inputParseFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return null;

        return new DateTimeOffset(BerlinTime.ToUtc(parsed), TimeSpan.Zero);
    }
}
