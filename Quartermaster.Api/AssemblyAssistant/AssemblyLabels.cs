using Quartermaster.Api.I18n;

namespace Quartermaster.Api.AssemblyAssistant;

/// <summary>i18n keys for roles, queues and phases, shared by console, overlay and the log export.</summary>
public static class AssemblyLabels {
    public static string RoleKey(SpeakerRole role) {
        return role switch {
            SpeakerRole.Pro => I18nKey.Ui.AssemblyConsole.RolePro,
            SpeakerRole.Contra => I18nKey.Ui.AssemblyConsole.RoleContra,
            SpeakerRole.Question => I18nKey.Ui.AssemblyConsole.RoleQuestion,
            SpeakerRole.Point => I18nKey.Ui.AssemblyConsole.RolePoint,
            SpeakerRole.MotionAuthor => I18nKey.Ui.AssemblyConsole.RoleMotionAuthor,
            SpeakerRole.Candidate => I18nKey.Ui.AssemblyConsole.RoleCandidate,
            _ => I18nKey.Ui.AssemblyConsole.RoleManual
        };
    }

    public static string QueueKey(QueueKind kind) {
        return kind switch {
            QueueKind.Point => I18nKey.Ui.AssemblyConsole.QueuePoint,
            QueueKind.Question => I18nKey.Ui.AssemblyConsole.QueueQuestion,
            QueueKind.Pro => I18nKey.Ui.AssemblyConsole.QueuePro,
            _ => I18nKey.Ui.AssemblyConsole.QueueContra
        };
    }

    public static string PhaseKey(AssemblyPhase phase) {
        return phase switch {
            AssemblyPhase.Presentation => I18nKey.Ui.AssemblyConsole.Phase.Presentation,
            AssemblyPhase.Questions => I18nKey.Ui.AssemblyConsole.Phase.Questions,
            AssemblyPhase.Debate => I18nKey.Ui.AssemblyConsole.Phase.Debate,
            AssemblyPhase.RoundEnd => I18nKey.Ui.AssemblyConsole.Phase.RoundEnd,
            AssemblyPhase.Statement => I18nKey.Ui.AssemblyConsole.Phase.Statement,
            AssemblyPhase.ClosingWord => I18nKey.Ui.AssemblyConsole.Phase.ClosingWord,
            AssemblyPhase.Vote => I18nKey.Ui.AssemblyConsole.Phase.Vote,
            AssemblyPhase.Candidacies => I18nKey.Ui.AssemblyConsole.Phase.Candidacies,
            AssemblyPhase.CandidatePresentation => I18nKey.Ui.AssemblyConsole.Phase.CandidatePresentation,
            AssemblyPhase.CandidateQuestions => I18nKey.Ui.AssemblyConsole.Phase.CandidateQuestions,
            AssemblyPhase.ElectionRunning => I18nKey.Ui.AssemblyConsole.Phase.ElectionRunning,
            AssemblyPhase.GeneralDebate => I18nKey.Ui.AssemblyConsole.Phase.GeneralDebate,
            _ => I18nKey.Ui.AssemblyConsole.Phase.None
        };
    }
}
