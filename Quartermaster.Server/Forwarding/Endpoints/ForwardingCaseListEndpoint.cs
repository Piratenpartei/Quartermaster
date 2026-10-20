using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class ForwardingCaseListEndpoint : Endpoint<ForwardingCaseListRequest, ForwardingCaseListResponse> {
    private readonly ForwardingCaseRepository _cases;
    private readonly ChapterRepository _chapters;
    private readonly PermissionContext _perms;

    public ForwardingCaseListEndpoint(ForwardingCaseRepository cases, ChapterRepository chapters, PermissionContext perms) {
        _cases = cases;
        _chapters = chapters;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forwarding/cases");
    }

    public override async Task HandleAsync(ForwardingCaseListRequest req, CancellationToken ct) {
        var kinds = ForwardingCaseAccess.VisibleKinds(_perms);
        if (kinds.Count == 0) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (cases, totalCount) = _cases.List(req.Status, kinds, req.Page, req.PageSize);
        var steps = _cases.GetSteps(cases.Select(c => c.Id).ToList());
        var names = new Dictionary<Guid, string>();
        string Name(Guid? id) {
            if (id == null)
                return "";
            if (!names.TryGetValue(id.Value, out var name)) {
                name = _chapters.Get(id.Value)?.Name ?? "";
                names[id.Value] = name;
            }
            return name;
        }

        var items = cases.Select(c => new ForwardingCaseListItemDTO {
            Id = c.Id,
            Reference = c.Reference,
            Kind = c.Kind,
            Status = c.Status,
            ChapterName = Name(c.DecidingChapterId ?? c.ApplicationChapterId),
            CreatedAt = c.CreatedAt.ToDtoUtc(),
            Steps = steps.GetValueOrDefault(c.Id, []).Select(s => new ForwardingStepDTO {
                Kind = s.Kind,
                Purpose = s.Purpose,
                TargetChapterName = Name(s.TargetChapterId),
                State = s.State,
                TicketUrl = s.TicketUrl,
                LastError = s.LastError,
                Attempts = s.Attempts
            }).ToList()
        }).ToList();
        await SendAsync(new ForwardingCaseListResponse { Items = items, TotalCount = totalCount, Page = req.Page, PageSize = req.PageSize }, cancellation: ct);
    }
}
