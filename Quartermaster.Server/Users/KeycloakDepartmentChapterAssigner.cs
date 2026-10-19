using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Users;

/// <summary>
/// Places a member in the chapter whose Keycloak department matches the account's <c>ekklesia_department</c>.
/// Keycloak only knows the state, so a member the import already put into a chapter inside that state keeps it.
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

    public void Assign(Member member, string? department) {
        var chapter = ChapterFor(department);
        if (chapter == null)
            return;
        if (member.ChapterId != null && _chapters.GetDescendantIds(chapter.Id).Contains(member.ChapterId.Value))
            return;

        _members.SetChapterId(member.Id, member.ChapterId, chapter.Id);
        member.ChapterId = chapter.Id;
    }
}
