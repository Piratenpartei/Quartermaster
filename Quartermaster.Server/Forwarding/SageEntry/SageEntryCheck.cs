using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding.SageEntry;

/// <summary>Reads the federal ticket to tell whether the SAGE entry is done; every configured signal must hold.</summary>
public class SageEntryCheck {
    private readonly IRedmineClient _redmine;

    public SageEntryCheck(IRedmineClient redmine) {
        _redmine = redmine;
    }

    public async Task<bool> IsEnteredAsync(ForwardingTarget target, int federalTicketId, RedmineConnectionSettings connection, CancellationToken ct) {
        // Without a signal every ticket would count as entered and the approval mail would go out unchecked.
        if (target.SageDoneStatusId == null && !target.SageAwaitReassignment)
            throw new RedmineException("Für die SAGE-Eintragung ist kein Signal für die erledigte Eintragung eingerichtet.", RedmineException.NotConfigured);

        var issue = await _redmine.GetIssueAsync(connection, federalTicketId, ct);
        if (target.SageDoneStatusId != null && issue.StatusId != target.SageDoneStatusId.Value)
            return false;
        if (!target.SageAwaitReassignment)
            return true;

        var account = await _redmine.GetCurrentUserAsync(connection, ct);
        return issue.AssigneeId == account.Id;
    }

    /// <summary>A ticket that already has the done status before the SAGE request would pass the first check and mail before the entry.</summary>
    public async Task RequireNotOnDoneStatusAsync(ForwardingTarget target, int federalTicketId, RedmineConnectionSettings connection, CancellationToken ct) {
        if (target.SageDoneStatusId == null)
            return;

        var issue = await _redmine.GetIssueAsync(connection, federalTicketId, ct);
        if (issue.StatusId == target.SageDoneStatusId.Value) {
            throw new RedmineException("Das Ticket der Bundesebene steht schon auf dem Erledigt-Status. Bitte den Status des Tickets oder den Erledigt-Status in der "
                + "Weiterleitung ändern und den Vorgang erneut versuchen.", RedmineException.NotConfigured);
        }
    }
}
