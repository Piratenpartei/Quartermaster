namespace Quartermaster.Api.AssemblyMotions;

public enum EligibilityProblem {
    None = 0,
    NotSignedIn = 1,
    NoLinkedMember = 2,
    NoMemberNumber = 3,
    Pending = 4,
    Anonymized = 5,
    Exited = 6,
    OutsideChapter = 7
}
