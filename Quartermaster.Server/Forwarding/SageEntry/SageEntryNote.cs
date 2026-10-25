using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding.SageEntry;

/// <summary>Writes the request to enter an approved applicant in SAGE, which follows the outcome note on the federal ticket.</summary>
public class SageEntryNote {
    private readonly ForwardingTicketRenderer _renderer;
    private readonly IRedmineClient _redmine;
    private readonly SageEntryGate _gate;
    private readonly DueSelectionRepository _dueSelections;

    public SageEntryNote(ForwardingTicketRenderer renderer, IRedmineClient redmine, SageEntryGate gate, DueSelectionRepository dueSelections) {
        _renderer = renderer;
        _redmine = redmine;
        _gate = gate;
        _dueSelections = dueSelections;
    }

    public async Task<string> BuildAsync(ForwardingCase forwardingCase, ForwardingStep step, MembershipApplication application, int federalTicketId,
        ForwardingTarget target, RedmineConnectionSettings connection, CancellationToken ct) {
        var dueRecord = application.DueSelectionId == null ? null : _dueSelections.Get(application.DueSelectionId.Value);
        var dto = application.ToDto(dueRecord?.ToDto());
        var reduction = _gate.ReductionOf(application);
        var entryDate = dto.EntryDate;

        var sage = new ForwardingSageModel {
            ProvisionalMemberNumber = "RM" + federalTicketId.ToString(CultureInfo.InvariantCulture),
            EntryDate = entryDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            EntryYear = entryDate.Year.ToString(CultureInfo.InvariantCulture),
            EntryMonth = SageDue.MonthName(entryDate.Month),
            ReductionResult = ResultLabel(reduction),
            DoneStatus = await DoneStatusAsync(target, connection, ct),
            DoneAccount = target.SageAwaitReassignment ? (await _redmine.GetCurrentUserAsync(connection, ct)).Name : ""
        };
        if (dueRecord != null) {
            var yearly = YearlyAmount(dueRecord, reduction);
            sage.YearlyAmount = ForwardingTicketRenderer.FormatAmount(yearly);
            sage.PartialAmount = ForwardingTicketRenderer.FormatAmount(SageDue.Partial(yearly, entryDate));
            if (reduction == ReductionOutcome.Outside)
                sage.PartialAmountIfReduced = ForwardingTicketRenderer.FormatAmount(SageDue.Partial(dueRecord.ReducedAmount, entryDate));
        }

        return await _renderer.RenderSageRequestAsync(forwardingCase, step, dto, sage);
    }

    /// <summary>A rejected reduction or one decided outside Quartermaster falls back to the due selected next to it, the rejection default.</summary>
    private static decimal YearlyAmount(DueSelection dueRecord, ReductionOutcome reduction) {
        return reduction switch {
            ReductionOutcome.Approved => dueRecord.ReducedAmount,
            ReductionOutcome.Rejected => dueRecord.SelectedDue,
            ReductionOutcome.Outside => dueRecord.SelectedDue,
            _ => DueAmount.Effective(dueRecord.ToDto())
        };
    }

    private static string ResultLabel(ReductionOutcome reduction) {
        return reduction switch {
            ReductionOutcome.Approved => "genehmigt",
            ReductionOutcome.Rejected => "abgelehnt",
            ReductionOutcome.Outside => "offen",
            _ => ""
        };
    }

    /// <summary>A done status Redmine does not know could never be reached, so the approval mail would never go out; that fails the step instead.</summary>
    private async Task<string> DoneStatusAsync(ForwardingTarget target, RedmineConnectionSettings connection, CancellationToken ct) {
        if (target.SageDoneStatusId == null)
            return "";

        var statuses = await _redmine.ListIssueStatusesAsync(connection, ct);
        var status = statuses.FirstOrDefault(s => s.Id == target.SageDoneStatusId.Value)
            ?? throw new RedmineException($"Den Status {target.SageDoneStatusId.Value} für die erledigte SAGE-Eintragung gibt es in Redmine nicht.", RedmineException.NotConfigured);
        return status.Name;
    }
}
