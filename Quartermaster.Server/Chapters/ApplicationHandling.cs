using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;

namespace Quartermaster.Server.Chapters;

/// <summary>Whether a chapter takes in membership applications itself, as opposed to being marked or forwarding them to a Redmine.</summary>
public class ApplicationHandling {
    private readonly ForwardingSettingsRepository _settings;

    public ApplicationHandling(ForwardingSettingsRepository settings) {
        _settings = settings;
    }

    public bool HandlesApplicationsInQuartermaster(Chapter chapter) {
        if (chapter.MarkFor(ChapterMarkPurpose.MemberAdministration) != null)
            return false;
        return _settings.GetChapter(chapter.Id)?.ApplicationMode == ForwardingMode.Quartermaster;
    }
}
