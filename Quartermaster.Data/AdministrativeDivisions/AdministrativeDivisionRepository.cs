using InterpolatedSql.Dapper;
using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Data.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivisionRepository : RepositoryBase<AdministrativeDivision> {
    private readonly DbContext _context;

    public AdministrativeDivisionRepository(DbContext context) {
        _context = context;
    }

    public AdministrativeDivision? Get(Guid id)
        => _context.AdministrativeDivisions.Where(ad => ad.Id == id).FirstOrDefault();

    public void Create(AdministrativeDivision division) => _context.Insert(division);

    public void CreateBulk(List<AdministrativeDivision> divisions) => _context.BulkCopy(divisions);

    public void SupplementDefaults(bool includeFromFiles = false) {
        if (includeFromFiles)
            SupplementFromFiles();

        if (Get(Guid.Empty) == null) {
            Create(new AdministrativeDivision {
                Id = Guid.Empty,
                Depth = 0,
                Name = "Null Island"
            });
        }
    }

    public void SupplementFromFiles()
        => AdministrativeDivisionLoader.Load("DE_Base.txt", "DE_PostCodes.txt", this);
    public void SupplementFromTestFiles()
        => AdministrativeDivisionLoader.Load("DE_Base_Test.txt", "DE_PostCodes_Test.txt", this);
}