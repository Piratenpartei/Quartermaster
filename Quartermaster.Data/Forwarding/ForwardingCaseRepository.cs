using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Data.Forwarding;

public class ForwardingCaseRepository {
    /// <summary>The notification log source of mails sent about a case: failure mails, and the receipt of an application only Redmine keeps.</summary>
    public const string MailSourceType = "ForwardingCase";

    private readonly DbContext _context;

    public ForwardingCaseRepository(DbContext context) {
        _context = context;
    }

    /// <summary>For services that write a case together with other records; repository calls inside must not start their own transaction.</summary>
    public DataConnectionTransaction BeginTransaction() {
        return _context.BeginTransaction();
    }

    /// <summary>Runs inside the caller's transaction (<see cref="BeginTransaction"/>).</summary>
    public void Create(ForwardingCase forwardingCase, IReadOnlyList<ForwardingStep> steps) {
        _context.Insert(forwardingCase);
        AddSteps(forwardingCase.Id, steps);
    }

    public void AddSteps(Guid caseId, IReadOnlyList<ForwardingStep> steps) {
        foreach (var step in steps) {
            step.CaseId = caseId;
            _context.Insert(step);
        }
    }

    /// <summary>Writes the routing of a case whose application only now got its chapter.</summary>
    public void UpdateRouting(ForwardingCase forwardingCase) {
        _context.ForwardingCases
            .Where(c => c.Id == forwardingCase.Id)
            .Set(c => c.ApplicationChapterId, forwardingCase.ApplicationChapterId)
            .Set(c => c.DecidingChapterId, forwardingCase.DecidingChapterId)
            .Set(c => c.StateChapterId, forwardingCase.StateChapterId)
            .Set(c => c.MembershipApplicationId, forwardingCase.MembershipApplicationId)
            .Set(c => c.DueSelectionId, forwardingCase.DueSelectionId)
            .Set(c => c.MotionId, forwardingCase.MotionId)
            .Set(c => c.PayloadJson, forwardingCase.PayloadJson)
            .Set(c => c.Status, forwardingCase.Status)
            .Set(c => c.CompletedAt, forwardingCase.CompletedAt)
            .Update();
    }

    public ForwardingCase? Get(Guid id) {
        return _context.ForwardingCases.FirstOrDefault(c => c.Id == id);
    }

    public List<ForwardingStep> GetSteps(Guid caseId) {
        return _context.ForwardingSteps
            .Where(s => s.CaseId == caseId)
            .OrderBy(s => s.Order)
            .ToList();
    }

    public Dictionary<Guid, List<ForwardingStep>> GetSteps(IReadOnlyCollection<Guid> caseIds) {
        return _context.ForwardingSteps
            .Where(s => caseIds.Contains(s.CaseId))
            .ToList()
            .GroupBy(s => s.CaseId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());
    }

    /// <summary>
    /// Cases whose next unfinished step the job should look at now: pending and due, or left in flight by an interrupted run. Oldest first.
    /// Later steps never count, because the job only runs them after the earlier ones.
    /// </summary>
    public List<Guid> ListCasesWithDueSteps(DateTime nowUtc, int limit) {
        return _context.ForwardingSteps
            .Where(s => (s.State == ForwardingStepState.Pending && (s.NextAttemptAt == null || s.NextAttemptAt <= nowUtc))
                || s.State == ForwardingStepState.InFlight)
            .Where(s => !_context.ForwardingSteps.Any(earlier => earlier.CaseId == s.CaseId
                && earlier.Order < s.Order
                && earlier.State != ForwardingStepState.Done
                && earlier.State != ForwardingStepState.Skipped))
            .InnerJoin(_context.ForwardingCases, (s, c) => s.CaseId == c.Id, (s, c) => c)
            .Where(c => c.Status == ForwardingCaseStatus.Open)
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.Id)
            .Distinct()
            .Take(limit)
            .ToList();
    }

    public void UpdateStep(ForwardingStep step) {
        WriteStep(_context.ForwardingSteps.Where(s => s.Id == step.Id), step);
    }

    /// <summary>Writes the step only while it is still in <paramref name="expectedState"/>; false when someone else changed it in the meantime.</summary>
    public bool UpdateStep(ForwardingStep step, ForwardingStepState expectedState) {
        return WriteStep(_context.ForwardingSteps.Where(s => s.Id == step.Id && s.State == expectedState), step) > 0;
    }

    private static int WriteStep(IQueryable<ForwardingStep> rows, ForwardingStep step) {
        return rows
            .Set(s => s.State, step.State)
            .Set(s => s.TicketId, step.TicketId)
            .Set(s => s.TicketUrl, step.TicketUrl)
            .Set(s => s.Attempts, step.Attempts)
            .Set(s => s.NextAttemptAt, step.NextAttemptAt)
            .Set(s => s.LastError, step.LastError)
            .Set(s => s.UpdatedAt, step.UpdatedAt)
            .Update();
    }

    /// <summary>Moves a case from <paramref name="from"/> to <paramref name="to"/>; false when its status was no longer <paramref name="from"/>.</summary>
    public bool SetCaseStatus(Guid id, ForwardingCaseStatus from, ForwardingCaseStatus to, DateTime? completedAt) {
        return _context.ForwardingCases
            .Where(c => c.Id == id && c.Status == from)
            .Set(c => c.Status, to)
            .Set(c => c.CompletedAt, completedAt)
            .Update() > 0;
    }

    public void SetMotion(Guid id, Guid motionId) {
        _context.ForwardingCases
            .Where(c => c.Id == id)
            .Set(c => c.MotionId, (Guid?)motionId)
            .Update();
    }

    public void SetRelatedCase(Guid id, Guid relatedCaseId) {
        _context.ForwardingCases
            .Where(c => c.Id == id)
            .Set(c => c.RelatedCaseId, (Guid?)relatedCaseId)
            .Update();
    }

    /// <summary>Deletes the submitted data of a case, including the bodies of mails logged for it, which carry the rendered ticket or the application.</summary>
    public void ClearPayload(Guid id) {
        using var tx = _context.BeginTransaction();
        _context.ForwardingCases
            .Where(c => c.Id == id)
            .Set(c => c.PayloadJson, (string?)null)
            .Update();
        _context.NotificationLogs
            .Where(l => l.SourceEntityType == MailSourceType && l.SourceEntityId == id)
            .Set(l => l.Body, (string?)null)
            .Update();
        tx.Commit();
    }

    public List<ForwardingCase> ListByMotion(Guid motionId) {
        return _context.ForwardingCases.Where(c => c.MotionId == motionId).ToList();
    }

    /// <summary>The cases of a kind that belong to a due selection (due selection cases) or an application (application cases).</summary>
    public List<ForwardingCase> ListByRecord(ForwardingCaseKind kind, Guid recordId) {
        var query = _context.ForwardingCases.Where(c => c.Kind == kind);
        query = kind == ForwardingCaseKind.Application
            ? query.Where(c => c.MembershipApplicationId == recordId)
            : query.Where(c => c.DueSelectionId == recordId);
        return query.ToList();
    }

    public (List<ForwardingCase> Items, int TotalCount) List(ForwardingCaseStatus? status, IReadOnlyCollection<ForwardingCaseKind> kinds, int page, int pageSize) {
        var query = _context.ForwardingCases.Where(c => kinds.Contains(c.Kind));
        if (status != null)
            query = query.Where(c => c.Status == status);

        var items = query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, query.Count());
    }
}
