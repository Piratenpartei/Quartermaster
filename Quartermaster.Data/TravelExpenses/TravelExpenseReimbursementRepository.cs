using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.TravelExpenses;

public class TravelExpenseReimbursementRepository {
    private readonly DbContext _context;

    public TravelExpenseReimbursementRepository(DbContext context) {
        _context = context;
    }

    public TravelExpenseReimbursement? Get(Guid id) {
        return _context.TravelExpenseReimbursements.FirstOrDefault(f => f.Id == id);
    }

    public List<TravelExpenseReimbursement> GetForOwner(Guid ownerUserId) {
        return _context.TravelExpenseReimbursements
            .Where(f => f.OwnerUserId == ownerUserId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToList();
    }

    public void Create(TravelExpenseReimbursement form) {
        _context.Insert(form);
    }

    public void Update(TravelExpenseReimbursement form) {
        _context.Update(form);
    }

    public void MarkSubmitted(Guid id, DateTime nowUtc) {
        _context.TravelExpenseReimbursements
            .Where(f => f.Id == id)
            .Set(f => f.Status, TravelExpenseStatus.Submitted)
            .Set(f => f.SubmittedAt, nowUtc)
            .Set(f => f.UpdatedAt, nowUtc)
            .Update();
    }

    public void Delete(Guid id) {
        _context.TravelExpenseReimbursements
            .Where(f => f.Id == id)
            .Delete();
    }
}
