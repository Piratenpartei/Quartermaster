namespace Quartermaster.Api.AssemblyAssistant;

/// <summary>Request queues in console display order; GO motions rank highest.</summary>
public enum QueueKind {
    Point = 0,
    Question = 1,
    Pro = 2,
    Contra = 3
}
