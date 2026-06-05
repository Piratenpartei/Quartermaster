using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Forms.ExpenseReimbursements;

public class ExpenseReimbursementRepository {
    private readonly DbContext _context;

    public ExpenseReimbursementRepository(DbContext context) {
        _context = context;
    }

    public void Create(ExpenseReimbursement form, IEnumerable<ExpenseReimbursementLineItem> items) {
        using var tx = _context.BeginTransaction();
        _context.Insert(form);
        foreach (var item in items) {
            item.ExpenseReimbursementId = form.Id;
            _context.Insert(item);
        }
        tx.Commit();
    }

    public ExpenseReimbursement? Get(Guid id)
        => _context.ExpenseReimbursements.Where(r => r.Id == id).FirstOrDefault();

    public List<ExpenseReimbursement> GetForOwner(Guid ownerUserId)
        => _context.ExpenseReimbursements
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToList();

    public Dictionary<Guid, decimal> GetLineItemTotals(IReadOnlyCollection<Guid> formIds) {
        if (formIds.Count == 0) {
            return new Dictionary<Guid, decimal>();
        }
        return _context.ExpenseReimbursementLineItems
            .Where(i => formIds.Contains(i.ExpenseReimbursementId))
            .GroupBy(i => i.ExpenseReimbursementId)
            .Select(g => new { Id = g.Key, Total = g.Sum(i => i.Value) })
            .ToDictionary(x => x.Id, x => x.Total);
    }

    public List<ExpenseReimbursementLineItem> GetLineItems(Guid formId)
        => _context.ExpenseReimbursementLineItems
            .Where(i => i.ExpenseReimbursementId == formId)
            .OrderBy(i => i.SortOrder)
            .ToList();

    public void ReplaceLineItems(Guid formId, IEnumerable<ExpenseReimbursementLineItem> items) {
        using var tx = _context.BeginTransaction();
        _context.ExpenseReimbursementLineItems.Where(i => i.ExpenseReimbursementId == formId).Delete();
        foreach (var item in items) {
            item.ExpenseReimbursementId = formId;
            _context.Insert(item);
        }
        tx.Commit();
    }

    public void UpdateDraft(ExpenseReimbursement form) {
        _context.ExpenseReimbursements
            .Where(r => r.Id == form.Id && r.Status == ExpenseReimbursementStatus.Draft)
            .Set(r => r.FirstName, form.FirstName)
            .Set(r => r.LastName, form.LastName)
            .Set(r => r.MemberNumber, form.MemberNumber)
            .Set(r => r.Street, form.Street)
            .Set(r => r.PostCode, form.PostCode)
            .Set(r => r.City, form.City)
            .Set(r => r.ChapterId, form.ChapterId)
            .Set(r => r.ChapterNameSnapshot, form.ChapterNameSnapshot)
            .Set(r => r.Function, form.Function)
            .Set(r => r.MotionDate, form.MotionDate)
            .Set(r => r.DonatedAmount, form.DonatedAmount)
            .Set(r => r.PayoutAmount, form.PayoutAmount)
            .Set(r => r.PayoutMethod, form.PayoutMethod)
            .Set(r => r.RequestsDonationReceipt, form.RequestsDonationReceipt)
            .Set(r => r.Iban, form.Iban)
            .Set(r => r.SignatureLocation, form.SignatureLocation)
            .Set(r => r.FormDate, form.FormDate)
            .Set(r => r.UpdatedAt, form.UpdatedAt)
            .Update();
    }

    public void MarkSubmitted(Guid id, DateTime submittedAt) {
        _context.ExpenseReimbursements
            .Where(r => r.Id == id && r.Status == ExpenseReimbursementStatus.Draft)
            .Set(r => r.Status, ExpenseReimbursementStatus.Submitted)
            .Set(r => r.SubmittedAt, (DateTime?)submittedAt)
            .Set(r => r.UpdatedAt, submittedAt)
            .Update();
    }

    public void DeleteDraft(Guid id) {
        using var tx = _context.BeginTransaction();
        _context.ExpenseReimbursementLineItems.Where(i => i.ExpenseReimbursementId == id).Delete();
        _context.ExpenseReimbursements
            .Where(r => r.Id == id && r.Status == ExpenseReimbursementStatus.Draft)
            .Delete();
        tx.Commit();
    }
}
