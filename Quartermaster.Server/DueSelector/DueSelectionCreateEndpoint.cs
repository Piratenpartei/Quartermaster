using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO> {
    private readonly DueSelectionRepository _dueSelectionRepository;

    public DueSelectionCreateEndpoint(DueSelectionRepository dueSelectionRepository) {
        _dueSelectionRepository = dueSelectionRepository;
    }

    public override void Configure() {
        Post("/api/dueselector");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
        var dueSelection = new DueSelection {
            FirstName = req.FirstName,
            LastName = req.LastName,
            EMail = req.EMail,
            MemberNumber = req.MemberNumber,
            SelectedValuation = req.SelectedValuation,
            YearlyIncome = req.YearlyIncome,
            MonthlyIncomeGroup = req.MonthlyIncomeGroup,
            ReducedAmount = req.ReducedAmount,
            SelectedDue = req.SelectedDue,
            ReducedJustification = req.ReducedJustification,
            ReducedTimeSpan = req.ReducedTimeSpan,
            IsDirectDeposit = req.IsDirectDeposit,
            AccountHolder = req.AccountHolder,
            IBAN = req.IBAN,
            PaymentSchedule = req.PaymentSchedule
        };
        _dueSelectionRepository.Create(dueSelection);
        await SendOkAsync(ct);
    }
}