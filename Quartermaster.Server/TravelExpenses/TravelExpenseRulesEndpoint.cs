using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRulesRequest {
    [QueryParam]
    public Guid? ChapterId { get; set; }

    [QueryParam]
    public DateOnly? Date { get; set; }
}

/// <summary>The rules a trip of the chapter on the date falls under; public, since anonymous applicants calculate in the browser.</summary>
public class TravelExpenseRulesEndpoint : Endpoint<TravelExpenseRulesRequest, TravelExpenseRulesDTO> {
    private readonly TravelExpenseService _service;

    public TravelExpenseRulesEndpoint(TravelExpenseService service) {
        _service = service;
    }

    public override void Configure() {
        Get("/api/forms/travel-expense/rules");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TravelExpenseRulesRequest req, CancellationToken ct) {
        await SendAsync(_service.RulesFor(req.ChapterId, req.Date ?? TravelExpenseService.Today()), cancellation: ct);
    }
}
