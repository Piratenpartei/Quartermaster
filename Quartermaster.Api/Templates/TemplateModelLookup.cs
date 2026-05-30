using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Api.Templates;

public static class TemplateModelLookup {
    public static string BuildForTemplate(string? identifier, bool allowsChapterFields, bool allowsMemberFields, bool allowsEventFields) {
        var models = new List<string>();
        var id = identifier ?? "";

        if (id.StartsWith("notifications.application_submitted.") || id.StartsWith("templates.membershipapplication.")) {
            models.Add("MembershipApplicationDetailDTO");
            models.Add("ChapterDTO");
        } else if (id.StartsWith("notifications.due_selection_submitted.") || id.StartsWith("templates.dueselection.")) {
            models.Add("DueSelectionDetailDTO");
            models.Add("ChapterDTO");
        } else if (id.StartsWith("notifications.motion_submitted.") || id.StartsWith("templates.submission.motion.")) {
            models.Add("MotionDetailDTO");
            models.Add("ChapterDTO");
        } else if (id.StartsWith("templates.member.welcome.")) {
            models.Add("MemberDetailDTO");
            models.Add("ChapterDTO");
        } else if (id.StartsWith("templates.submission.dueselection.")) {
            models.Add("DueSelectionDetailDTO");
        } else if (id.StartsWith("templates.submission.membershipapplication.")) {
            models.Add("MembershipApplicationDetailDTO");
            models.Add("ChapterDTO");
        }

        if (id.StartsWith("templates.submission.") && id.EndsWith(".confirmation.email"))
            models.Add("TemplateConfirmationDTO");

        if (allowsChapterFields && !models.Contains("ChapterDTO"))
            models.Add("ChapterDTO");
        if (allowsMemberFields && !models.Contains("MemberDetailDTO"))
            models.Add("MemberDetailDTO");
        if (allowsEventFields && !models.Contains("EventDetailDTO"))
            models.Add("EventDetailDTO");

        return string.Join(",", models);
    }
}
