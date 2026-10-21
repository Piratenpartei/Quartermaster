using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignCreateEndpoint : Endpoint<SignatureCampaignSaveRequest, SignatureCampaignDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly ChapterRepository _chapters;
    private readonly SignatureCampaignDtoBuilder _builder;
    private readonly SignatureCampaignAccess _access;
    private readonly AuditLogRepository _auditLog;

    public CampaignCreateEndpoint(SignatureCampaignRepository campaigns, ChapterRepository chapters, SignatureCampaignDtoBuilder builder,
        SignatureCampaignAccess access, AuditLogRepository auditLog) {
        _campaigns = campaigns;
        _chapters = chapters;
        _builder = builder;
        _access = access;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Post("/api/support-signatures/campaigns");
    }

    public override async Task HandleAsync(SignatureCampaignSaveRequest req, CancellationToken ct) {
        if (!_access.CanManageChapter(req.ChapterId)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_chapters.Get(req.ChapterId) == null) {
            AddError(I18nKey.Error.SupportSignatures.Campaign.ChapterNotFound);
            await SendErrorsAsync(404, ct);
            return;
        }

        var campaign = new SignatureCampaign {
            ChapterId = req.ChapterId,
            CreatedAt = DateTime.UtcNow
        };
        SignatureCampaignSettings.Apply(campaign, req);
        _campaigns.Create(campaign);
        _auditLog.LogCreated("SignatureCampaign", campaign.Id);

        await SendAsync(_builder.Build(campaign), cancellation: ct);
    }
}
