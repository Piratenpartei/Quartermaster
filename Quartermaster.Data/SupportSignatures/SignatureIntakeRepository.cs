using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.SupportSignatures;

public class SignatureIntakeRepository {
    private readonly DbContext _context;

    public SignatureIntakeRepository(DbContext context) {
        _context = context;
    }

    public SignatureIntake? Get(Guid id) {
        return _context.SignatureIntakes.FirstOrDefault(i => i.Id == id);
    }

    public List<SignatureIntake> List(Guid campaignId) {
        return _context.SignatureIntakes
            .Where(i => i.CampaignId == campaignId)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    public void Create(SignatureIntake intake) {
        _context.Insert(intake);
    }

    public void Update(SignatureIntake intake) {
        _context.SignatureIntakes
            .Where(i => i.Id == intake.Id)
            .Set(i => i.AdministrativeDivisionId, intake.AdministrativeDivisionId)
            .Set(i => i.Count, intake.Count)
            .Set(i => i.AlreadyCertified, intake.AlreadyCertified)
            .Set(i => i.ReceivedOn, intake.ReceivedOn)
            .Set(i => i.Note, intake.Note)
            .Update();
    }

    public void Delete(Guid id) {
        _context.SignatureIntakes
            .Where(i => i.Id == id)
            .Delete();
    }
}
