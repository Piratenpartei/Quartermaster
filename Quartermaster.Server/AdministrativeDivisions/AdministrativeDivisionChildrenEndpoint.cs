using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdministrativeDivisionChildrenRequest {
    public Guid Id { get; set; }
}

public class AdministrativeDivisionChildrenEndpoint : Endpoint<AdministrativeDivisionChildrenRequest, List<AdministrativeDivisionDTO>> {
    private readonly AdministrativeDivisionRepository _repository;

    public AdministrativeDivisionChildrenEndpoint(AdministrativeDivisionRepository repository) {
        _repository = repository;
    }

    public override void Configure() {
        Get("/api/administrativedivisions/{Id}/children");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AdministrativeDivisionChildrenRequest req, CancellationToken ct) {
        var children = _repository.GetChildren(req.Id);
        var dtos = children.Select(ad => new AdministrativeDivisionDTO {
            Id = ad.Id,
            ParentId = ad.ParentId,
            Name = ad.Name,
            Depth = ad.Depth,
            AdminCode = ad.AdminCode,
            PostCodes = ad.PostCodes
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
