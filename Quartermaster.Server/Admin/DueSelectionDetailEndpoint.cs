using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.Admin;

public class DueSelectionDetailRequest {
    public Guid Id { get; set; }
}

public class DueSelectionDetailEndpoint
    : Endpoint<DueSelectionDetailRequest, DueSelectionDetailDTO> {

    private readonly DueSelectionRepository _dueSelectionRepo;

    public DueSelectionDetailEndpoint(DueSelectionRepository dueSelectionRepo) {
        _dueSelectionRepo = dueSelectionRepo;
    }

    public override void Configure() {
        Get("/api/admin/dueselections/{Id}");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(DueSelectionDetailRequest req, CancellationToken ct) {
        var ds = _dueSelectionRepo.Get(req.Id);
        if (ds == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new DueSelectionDetailDTO {
            Id = ds.Id,
            FirstName = ds.FirstName,
            LastName = ds.LastName,
            EMail = ds.EMail,
            MemberNumber = ds.MemberNumber,
            SelectedValuation = (int)ds.SelectedValuation,
            YearlyIncome = ds.YearlyIncome,
            MonthlyIncomeGroup = ds.MonthlyIncomeGroup,
            ReducedAmount = ds.ReducedAmount,
            SelectedDue = ds.SelectedDue,
            ReducedJustification = ds.ReducedJustification,
            ReducedTimeSpan = (int)ds.ReducedTimeSpan,
            IsDirectDeposit = ds.IsDirectDeposit,
            AccountHolder = ds.AccountHolder,
            IBAN = ds.IBAN,
            PaymentSchedule = (int)ds.PaymentSchedule,
            Status = (int)ds.Status,
            ProcessedAt = ds.ProcessedAt
        }, cancellation: ct);
    }
}
