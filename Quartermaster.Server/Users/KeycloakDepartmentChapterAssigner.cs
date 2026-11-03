using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Users;

/// <summary>
/// Places a member without a chapter in the chapter whose Keycloak department matches the account's <c>ekklesia_department</c>.
/// The member import is the authority for chapters, so a member that already has one keeps it.
/// </summary>
public class KeycloakDepartmentChapterAssigner {
    private readonly ChapterRepository _chapters;
    private readonly MemberRepository _members;

    public KeycloakDepartmentChapterAssigner(ChapterRepository chapters, MemberRepository members) {
        _chapters = chapters;
        _members = members;
    }

    public Chapter? ChapterFor(string? department) {
        return string.IsNullOrWhiteSpace(department) ? null : _chapters.GetByKeycloakDepartment(department);
    }

    /// <summary>Returns whether the member's chapter changed.</summary>
    public bool Assign(Member member, string? department) {
        if (member.ChapterId != null)
            return false;
        var chapter = ChapterFor(department);
        if (chapter == null)
            return false;

        _members.SetChapterId(member.Id, member.ChapterId, chapter.Id);
        member.ChapterId = chapter.Id;
        return true;
    }
}
