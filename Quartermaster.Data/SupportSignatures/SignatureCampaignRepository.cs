using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;

namespace Quartermaster.Data.SupportSignatures;

public class SignatureCampaignRepository {
    private readonly DbContext _context;

    public SignatureCampaignRepository(DbContext context) {
        _context = context;
    }

    public DataConnectionTransaction BeginTransaction() {
        return _context.BeginTransaction();
    }

    public SignatureCampaign? Get(Guid id) {
        return _context.SignatureCampaigns.FirstOrDefault(c => c.Id == id);
    }

    /// <summary>Locks the campaign row until the transaction ends, so changes to its forms and shipments run one after another.</summary>
    public void Lock(Guid id) {
        _context.Query<Guid>(
                $"SELECT Id FROM {SignatureCampaign.TableName} WHERE Id = @id FOR UPDATE",
                new DataParameter("id", id)).FirstOrDefault();
    }

    /// <param name="chapterIds">The chapters to list; null for all.</param>
    public List<SignatureCampaign> List(List<Guid>? chapterIds, bool archived) {
        return _context.SignatureCampaigns
            .Where(c => chapterIds == null || chapterIds.Contains(c.ChapterId))
            .Where(c => archived ? c.ArchivedAt != null : c.ArchivedAt == null)
            .OrderBy(c => c.Deadline)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public void Create(SignatureCampaign campaign) {
        _context.Insert(campaign);
    }

    public void UpdateSettings(SignatureCampaign campaign) {
        _context.SignatureCampaigns
            .Where(c => c.Id == campaign.Id)
            .Set(c => c.Name, campaign.Name)
            .Set(c => c.Target, campaign.Target)
            .Set(c => c.MarginPercent, campaign.MarginPercent)
            .Set(c => c.Deadline, campaign.Deadline)
            .Set(c => c.OverdueDays, campaign.OverdueDays)
            .Set(c => c.Visibility, campaign.Visibility)
            .Set(c => c.MemberNotice, campaign.MemberNotice)
            .Set(c => c.PostalAddress, campaign.PostalAddress)
            .Set(c => c.ContactEmail, campaign.ContactEmail)
            .Update();
    }

    public void SetArchivedAt(Guid id, DateTime? archivedAt) {
        _context.SignatureCampaigns
            .Where(c => c.Id == id)
            .Set(c => c.ArchivedAt, archivedAt)
            .Update();
    }

    public void SetForm(Guid id, string? fileName) {
        _context.SignatureCampaigns
            .Where(c => c.Id == id)
            .Set(c => c.HasForm, fileName != null)
            .Set(c => c.FormFileName, fileName)
            .Update();
    }
}
