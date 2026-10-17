namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>Default speaking times in seconds (GO BPT §§ 11 to 13).</summary>
public record AssemblyTimes(int Presentation, int Speech, int Question, int Statement, int ClosingWord,
    int CandidatePresentation, int CandidatePresentationRepeat, int CandidateQuestion, int CandidateAnswer) {

    public static AssemblyTimes Default { get; } = new(300, 180, 30, 60, 180, 300, 180, 30, 120);
}
