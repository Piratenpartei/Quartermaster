using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using DataDueSelector = Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO> {
    private readonly DataDueSelector.DueSelectionRepository _dueSelectionRepository;

    public DueSelectionCreateEndpoint(DataDueSelector.DueSelectionRepository dueSelectionRepository) {
        _dueSelectionRepository = dueSelectionRepository;
    }

    public override void Configure() {
        Post("/api/dueselector");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
        var dueSelection = new DataDueSelector.DueSelection {
            FirstName = req.FirstName,
            LastName = req.LastName,
            EMail = req.EMail,
            MemberNumber = req.MemberNumber,
            SelectedValuation = (DataDueSelector.SelectedValuation)(int)req.SelectedValuation,
            YearlyIncome = req.YearlyIncome,
            MonthlyIncomeGroup = req.MonthlyIncomeGroup,
            ReducedAmount = req.ReducedAmount,
            SelectedDue = req.SelectedDue,
            ReducedJustification = req.ReducedJustification,
            ReducedTimeSpan = (DataDueSelector.ReducedTimeSpan)(int)req.ReducedTimeSpan,
            IsDirectDeposit = req.IsDirectDeposit,
            AccountHolder = req.AccountHolder,
            IBAN = req.IBAN,
            PaymentSchedule = (DataDueSelector.PaymentScedule)(int)req.PaymentScedule
        };
        _dueSelectionRepository.Create(dueSelection);
        await SendOkAsync(ct);
    }
}