using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Options;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Receipts;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignFormUploadRequest {
    public Guid Id { get; set; }
    public IFormFile? File { get; set; }
}

public class CampaignFormUploadEndpoint : Endpoint<CampaignFormUploadRequest, SignatureCampaignDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SupportSignatureFormStorage _storage;
    private readonly OptionRepository _options;
    private readonly SignatureCampaignDtoBuilder _builder;
    private readonly SignatureCampaignAccess _access;
    private readonly AuditLogRepository _auditLog;

    public CampaignFormUploadEndpoint(SignatureCampaignRepository campaigns, SupportSignatureFormStorage storage, OptionRepository options,
        SignatureCampaignDtoBuilder builder, SignatureCampaignAccess access, AuditLogRepository auditLog) {
        _campaigns = campaigns;
        _storage = storage;
        _options = options;
        _builder = builder;
        _access = access;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Put("/api/support-signatures/campaigns/{Id}/form");
        AllowFileUploads();
    }

    public override async Task HandleAsync(CampaignFormUploadRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.File == null || req.File.Length == 0) {
            ThrowError(I18nKey.Error.SupportSignatures.Form.NoFile);
            return;
        }
        var maxMegabytes = MaxMegabytes();
        if (req.File.Length > maxMegabytes * 1024L * 1024L) {
            ThrowError(I18nParams.With(I18nKey.Error.SupportSignatures.Form.TooLarge, ("max", maxMegabytes.ToString())));
            return;
        }
        if (!await IsPdfAsync(req.File, ct)) {
            ThrowError(I18nKey.Error.SupportSignatures.Form.NotPdf);
            return;
        }

        await using (var content = req.File.OpenReadStream()) {
            await _storage.SaveAsync(campaign.Id, content, ct);
        }
        var fileName = Path.GetFileName(req.File.FileName);
        campaign.FormFileName = fileName.Length <= SignatureCampaign.FormFileNameMaxLength ? fileName : fileName[^SignatureCampaign.FormFileNameMaxLength..];
        campaign.HasForm = true;
        _campaigns.SetForm(campaign.Id, campaign.FormFileName);
        _auditLog.Log("SignatureCampaign", campaign.Id, "FormUploaded", "FormFileName", null, campaign.FormFileName);

        await SendAsync(_builder.Build(campaign), cancellation: ct);
    }

    private int MaxMegabytes() {
        var raw = _options.GetGlobalValue(SupportSignatureOptions.FormMaxMegabytes)?.Value;
        return int.TryParse(raw, out var value) && value > 0 ? value : SupportSignatureOptions.DefaultFormMaxMegabytes;
    }

    private static async Task<bool> IsPdfAsync(IFormFile file, CancellationToken ct) {
        await using var stream = file.OpenReadStream();
        var header = new byte[ReceiptFileType.HeaderLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        return ReceiptFileType.Detect(header.AsSpan(0, read)) == ReceiptFileType.Pdf;
    }
}
