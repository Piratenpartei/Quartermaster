using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Receipts;

public class UserReceiptRepository {
    private readonly DbContext _context;

    public UserReceiptRepository(DbContext context) {
        _context = context;
    }

    public List<UserReceipt> ListForOwner(Guid ownerUserId) {
        return _context.UserReceipts
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.UploadedAt)
            .ToList();
    }

    public int CountForOwner(Guid ownerUserId) {
        return _context.UserReceipts.Count(r => r.OwnerUserId == ownerUserId);
    }

    /// <summary>The receipt when it belongs to the owner; null otherwise, so foreign ids look like missing ones.</summary>
    public UserReceipt? GetOwned(Guid id, Guid ownerUserId) {
        return _context.UserReceipts.FirstOrDefault(r => r.Id == id && r.OwnerUserId == ownerUserId);
    }

    public List<UserReceipt> GetOwned(IReadOnlyCollection<Guid> ids, Guid ownerUserId) {
        return _context.UserReceipts
            .Where(r => ids.Contains(r.Id) && r.OwnerUserId == ownerUserId)
            .ToList();
    }

    public void Create(UserReceipt receipt) {
        _context.Insert(receipt);
    }

    public void Rename(Guid id, string label) {
        _context.UserReceipts
            .Where(r => r.Id == id)
            .Set(r => r.Label, label)
            .Update();
    }

    public void Delete(Guid id) {
        _context.UserReceipts
            .Where(r => r.Id == id)
            .Delete();
    }
}
