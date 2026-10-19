using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRenderValidator : Validator<TravelExpenseRenderRequest> {
    public TravelExpenseRenderValidator() {
        Include(new TravelExpenseInputRules());
    }
}

/// <summary>Distinct from the create request so its validator binds only here.</summary>
public class TravelExpenseRenderRequest : TravelExpenseInputDTO { }

/// <summary>The PDF for an application kept in the browser; nothing is stored and no receipts are attached.</summary>
public class TravelExpenseRenderEndpoint : Endpoint<TravelExpenseRenderRequest> {
    private readonly TravelExpensePdfService _pdf;

    public TravelExpenseRenderEndpoint(TravelExpensePdfService pdf) {
        _pdf = pdf;
    }

    public override void Configure() {
        Post("/api/forms/travel-expense/render");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(TravelExpenseRenderRequest req, CancellationToken ct) {
        var pdf = await _pdf.RenderAsync(req, ownerUserId: null);
        await SendBytesAsync(pdf, fileName: "reisekosten.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
