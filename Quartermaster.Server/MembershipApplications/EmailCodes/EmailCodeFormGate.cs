using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.PublicForms;

namespace Quartermaster.Server.MembershipApplications.EmailCodes;

public class EmailCodeFormGate {
    private readonly PublicFormSettings _publicForms;
    private readonly PermissionContext _perms;

    public EmailCodeFormGate(PublicFormSettings publicForms, PermissionContext perms) {
        _publicForms = publicForms;
        _perms = perms;
    }

    /// <summary>The error key and HTTP status that refuse the request, or null when the application form is usable.</summary>
    public (string Key, int Status)? Refusal(string email) {
        if (!_publicForms.IsMembershipApplicationReady())
            return (I18nKey.Error.Admin.Application.NotSetUp, 400);
        if (!_publicForms.IsMembershipApplicationEnabled() && !_perms.HasInAnyChapter(PermissionIdentifier.ProcessApplications))
            return (I18nKey.Error.PublicForm.Disabled, 403);
        if (!EmailCodeService.IsAcceptableAddress(email))
            return (I18nKey.Error.EmailCode.AddressInvalid, 400);
        return null;
    }
}
