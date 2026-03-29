using LinqToDB;
using System;

namespace Quartermaster.Data.MembershipApplications;

public class MembershipApplicationRepository {
    private readonly DbContext _context;

    public MembershipApplicationRepository(DbContext context) {
        _context = context;
    }

    public void Create(MembershipApplication application) => _context.Insert(application);
}
