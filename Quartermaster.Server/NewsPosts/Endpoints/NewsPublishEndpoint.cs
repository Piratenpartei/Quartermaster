using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsPublishBody : PublishRequest {
    public Guid Id { get; set; }
}

public class NewsPublishEndpoint : Endpoint<NewsPublishBody, PostPublicationDTO> {
    private readonly NewsItemRepository _itemRepo;
    private readonly PostPublicationRepository _publicationRepo;
    private readonly IPostizClient _postiz;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;
    private readonly NewsPostsSettings _settings;

    public NewsPublishEndpoint(NewsItemRepository itemRepo, PostPublicationRepository publicationRepo, IPostizClient postiz,
        AuditLogRepository auditLog, PermissionContext perms, NewsPostsSettings settings) {
        _itemRepo = itemRepo;
        _publicationRepo = publicationRepo;
        _postiz = postiz;
        _auditLog = auditLog;
        _perms = perms;
        _settings = settings;
    }

    public override void Configure() {
        Post("/api/newsposts/items/{Id}/publish");
    }

    public override async Task HandleAsync(NewsPublishBody req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var item = _itemRepo.Get(req.Id);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_settings.HasPostiz) {
            AddError("Postiz ist nicht konfiguriert.");
            await SendErrorsAsync(409, ct);
            return;
        }

        var text = req.Text.Trim();
        var error = ValidateRequest(text, req);
        if (error != null) {
            AddError(error);
            await SendErrorsAsync(400, ct);
            return;
        }

        // Nothing is claimed yet at this point, so a channel-load failure here is safe to
        // report and let the reviewer retry, unlike a failure after TryClaimForPosting below.
        List<PostizChannel> selected;
        try {
            (selected, error) = await ResolveChannelsAsync(req.ChannelIds, ct);
        } catch (PostizException ex) {
            Logger.LogWarning(ex, "Loading Postiz channels for publish failed");
            AddError("Die Kanäle konnten nicht von Postiz geladen werden. Bitte später erneut versuchen.");
            await SendErrorsAsync(502, ct);
            return;
        } catch (HttpRequestException ex) {
            Logger.LogWarning(ex, "Loading Postiz channels for publish failed");
            AddError("Die Kanäle konnten nicht von Postiz geladen werden. Bitte später erneut versuchen.");
            await SendErrorsAsync(502, ct);
            return;
        }
        if (error != null) {
            AddError(error);
            await SendErrorsAsync(400, ct);
            return;
        }

        if (!_itemRepo.TryClaimForPosting(item.Id)) {
            AddError("Diese Meldung wird bereits veröffentlicht oder wurde schon veröffentlicht.");
            await SendErrorsAsync(409, ct);
            return;
        }

        var scheduledUtc = req.ScheduledFor?.UtcDateTime;
        var publication = new PostPublication {
            NewsItemId = item.Id,
            CandidateId = req.CandidateId,
            FinalText = text,
            ChannelIds = JsonSerializer.Serialize(selected.Select(c => c.Id)),
            ScheduledFor = scheduledUtc,
            ApprovedByUserId = _perms.UserId.Value,
            ApprovedAt = DateTime.UtcNow
        };

        // The item is claimed (Posting) from here on. A client disconnect must not abort the
        // Postiz call or the bookkeeping that follows, or the item is stranded in Posting with
        // no recovery path, so the rest of the request runs with CancellationToken.None.
        List<string> postIds;
        try {
            postIds = await _postiz.CreatePostAsync(text, selected, scheduledUtc, CancellationToken.None);
        } catch (Exception ex) {
            await RecordFailureAsync(item, publication, ex);
            return;
        }

        await RecordSuccessAsync(item, publication, postIds, text, selected, req.ScheduledFor);
    }

    private async Task RecordFailureAsync(NewsItem item, PostPublication publication, Exception ex) {
        Logger.LogError(ex, "Publishing news item {Id} to Postiz failed", item.Id);
        publication.Status = PostPublicationStatus.Failed;
        publication.Error = ex.Message;
        _publicationRepo.Create(publication);
        _itemRepo.SetStatus(item.Id, NewsItemStatus.Generated);
        AddError("Postiz hat die Veröffentlichung abgelehnt oder nicht beantwortet. Bitte in Postiz prüfen, bevor du es erneut versuchst.");
        await SendErrorsAsync(502, CancellationToken.None);
    }

    private async Task RecordSuccessAsync(NewsItem item, PostPublication publication, List<string> postIds, string text,
        List<PostizChannel> selected, DateTimeOffset? scheduledFor) {
        publication.PostizPostIds = JsonSerializer.Serialize(postIds);
        publication.Status = PostPublicationStatus.Sent;

        // The post is already public at this point, so the item must stay Posted even if the
        // publication record or audit entry below fails to write; such a failure is logged
        // only, never surfaced to the caller as an error.
        try {
            _itemRepo.SetStatus(item.Id, NewsItemStatus.Posted);
            _publicationRepo.Create(publication);
            _auditLog.Log("NewsItem", item.Id, "Published", newValue: publication.ChannelIds);
        } catch (Exception ex) {
            Logger.LogError(ex, "Recording the publication for news item {Id} failed after Postiz accepted the post", item.Id);
        }

        await SendAsync(new PostPublicationDTO {
            Id = publication.Id,
            NewsItemId = item.Id,
            NewsTitle = item.Title,
            FinalText = text,
            ChannelIds = selected.Select(c => c.Id).ToList(),
            ScheduledFor = scheduledFor,
            Succeeded = true,
            ApprovedAt = new DateTimeOffset(publication.ApprovedAt, TimeSpan.Zero)
        }, cancellation: CancellationToken.None);
    }

    private static string? ValidateRequest(string text, PublishRequest req) {
        if (!PostText.IsValid(text))
            return $"Der Text muss zwischen 1 und {PostText.MaxLength} Zeichen lang sein.";
        if (req.ChannelIds.Count == 0)
            return "Bitte mindestens einen Kanal auswählen.";
        if (req.ScheduledFor != null && req.ScheduledFor.Value <= DateTimeOffset.UtcNow)
            return "Der geplante Zeitpunkt liegt in der Vergangenheit.";
        return null;
    }

    private async Task<(List<PostizChannel> Channels, string? Error)> ResolveChannelsAsync(List<string> ids, CancellationToken ct) {
        var available = (await _postiz.GetChannelsAsync(ct)).ToDictionary(c => c.Id);
        var selected = new List<PostizChannel>();
        foreach (var id in ids.Distinct()) {
            if (!available.TryGetValue(id, out var channel) || channel.Disabled)
                return ([], "Ein ausgewählter Kanal ist in Postiz nicht (mehr) verfügbar. Bitte die Kanäle neu laden.");
            selected.Add(channel);
        }
        return (selected, null);
    }
}
