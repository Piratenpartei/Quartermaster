using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterMails;

public class ChapterMailDeleteEndpoint : Endpoint<ChapterMailIdRequest> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly PermissionContext _perms;

    public ChapterMailDeleteEndpoint(ChapterMailRepository mailRepo, PermissionContext perms) {
        _mailRepo = mailRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/chapter-mail/{Id}");
    }

    public override async Task HandleAsync(ChapterMailIdRequest req, CancellationToken ct) {
        var mail = _mailRepo.Get(req.Id);
        if (mail == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(mail.ChapterId, PermissionIdentifier.ManageChapterMail)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (mail.IsArchived) {
            await SendOkAsync(ct);
            return;
        }

        _mailRepo.Delete(mail);
        await SendOkAsync(ct);
    }
}
