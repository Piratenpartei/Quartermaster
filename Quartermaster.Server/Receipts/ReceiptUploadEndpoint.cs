using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Receipts;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Receipts;

public class ReceiptUploadRequest {
    public IFormFile File { get; set; } = default!;
    public string? Label { get; set; }
}

public class ReceiptUploadEndpoint : Endpoint<ReceiptUploadRequest, UserReceiptDTO> {
    private readonly UserReceiptRepository _receipts;
    private readonly ReceiptStorage _storage;
    private readonly ReceiptLimits _limits;
    private readonly PermissionContext _perms;

    public ReceiptUploadEndpoint(UserReceiptRepository receipts, ReceiptStorage storage, ReceiptLimits limits, PermissionContext perms) {
        _receipts = receipts;
        _storage = storage;
        _limits = limits;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/receipts");
        AllowFileUploads();
    }

    public override async Task HandleAsync(ReceiptUploadRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var ownerId = _perms.UserId.Value;

        if (req.File == null || req.File.Length == 0) {
            ThrowError(I18nKey.Error.Receipt.NoFile);
            return;
        }
        if (req.File.Length > _limits.MaxFileMegabytes() * 1024L * 1024L) {
            ThrowError(I18nParams.With(I18nKey.Error.Receipt.TooLarge, ("max", _limits.MaxFileMegabytes().ToString())));
            return;
        }

        var maxCount = _limits.MaxCount();
        if (maxCount != null && _receipts.CountForOwner(ownerId) >= maxCount.Value) {
            ThrowError(I18nParams.With(I18nKey.Error.Receipt.LimitReached, ("max", maxCount.Value.ToString())));
            return;
        }

        var type = await DetectTypeAsync(req.File, ct);
        if (type == null) {
            ThrowError(I18nKey.Error.Receipt.UnsupportedType);
            return;
        }

        var receipt = new UserReceipt {
            OwnerUserId = ownerId,
            FileName = Truncate(Path.GetFileName(req.File.FileName), UserReceipt.FileNameMaxLength),
            Label = Truncate((req.Label ?? Path.GetFileNameWithoutExtension(req.File.FileName)).Trim(), UserReceipt.LabelMaxLength),
            ContentType = type.ContentType,
            SizeBytes = req.File.Length,
            UploadedAt = DateTime.UtcNow
        };
        await using (var content = req.File.OpenReadStream()) {
            receipt.StoredName = await _storage.SaveAsync(ownerId, receipt.Id, type, content, ct);
        }
        try {
            _receipts.Create(receipt);
        } catch (Exception ex) {
            Logger.LogError(ex, "Storing receipt {ReceiptId} failed, removing its file", receipt.Id);
            _storage.Delete(ownerId, receipt.StoredName);
            throw;
        }

        await SendAsync(receipt.ToDto(), cancellation: ct);
    }

    private static async Task<ReceiptFileType?> DetectTypeAsync(IFormFile file, CancellationToken ct) {
        await using var stream = file.OpenReadStream();
        var header = new byte[ReceiptFileType.HeaderLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        return ReceiptFileType.Detect(header.AsSpan(0, read));
    }

    private static string Truncate(string value, int maxLength) {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
