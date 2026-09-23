using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsPublicationListEndpoint : EndpointWithoutRequest<List<PostPublicationDTO>> {
    private const int Limit = 200;

    private readonly PostPublicationRepository _publicationRepo;
    private readonly NewsItemRepository _itemRepo;
    private readonly UserRepository _userRepo;
    private readonly PermissionContext _perms;

    public NewsPublicationListEndpoint(PostPublicationRepository publicationRepo, NewsItemRepository itemRepo, UserRepository userRepo, PermissionContext perms) {
        _publicationRepo = publicationRepo;
        _itemRepo = itemRepo;
        _userRepo = userRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/publications");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var publications = _publicationRepo.GetRecent(Limit);
        var items = publications
            .Select(p => p.NewsItemId)
            .Distinct()
            .Select(id => _itemRepo.Get(id))
            .Where(item => item != null)
            .ToDictionary(item => item!.Id, item => item!);
        var approvers = publications
            .Select(p => p.ApprovedByUserId)
            .Distinct()
            .Select(id => _userRepo.Get(id))
            .Where(user => user != null)
            .ToDictionary(user => user!.Id, user => user!);

        var dtos = publications
            .Select(p => new PostPublicationDTO {
                Id = p.Id,
                NewsItemId = p.NewsItemId,
                NewsTitle = items.GetValueOrDefault(p.NewsItemId)?.Title ?? "",
                FinalText = p.FinalText,
                ChannelIds = JsonSerializer.Deserialize<List<string>>(p.ChannelIds) ?? [],
                ScheduledFor = p.ScheduledFor == null
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(p.ScheduledFor.Value, DateTimeKind.Utc)),
                Succeeded = p.Status == PostPublicationStatus.Sent,
                Error = p.Error,
                ApprovedBy = approvers.TryGetValue(p.ApprovedByUserId, out var approver) ? $"{approver.FirstName} {approver.LastName}" : "",
                ApprovedAt = new DateTimeOffset(DateTime.SpecifyKind(p.ApprovedAt, DateTimeKind.Utc))
            })
            .ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
