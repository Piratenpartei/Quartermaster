using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Data.Abstract;
using System;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivisionRepository : RepositoryBase<AdministrativeDivision> {
    private readonly DbContext _context;

    internal AdministrativeDivisionRepository(DbContext context) {
        _context = context;
    }

    public AdministrativeDivision? Get(Guid id)
        => _context.AdministrativeDivisions.Where(ad => ad.Id == id).FirstOrDefault();

    public void Create(AdministrativeDivision division) => _context.Insert(division);

    public void SupplementDefaults() {
        if (Get(Guid.Empty) == null) {
            Create(new AdministrativeDivision {
                Id = Guid.Empty,
                Depth = 0,
                Name = "Null Island"
            });
        }
    }
}