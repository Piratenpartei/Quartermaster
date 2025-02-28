using InterpolatedSql.Dapper;
using Quartermaster.Data.Abstract;
using System;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivisionRepository : RepositoryBase<AdministrativeDivision> {
    private readonly SqlContext _context;

    internal AdministrativeDivisionRepository(SqlContext context) {
        _context = context;
    }

    public AdministrativeDivision? Get(Guid id) {
        using var con = _context.GetConnection();
        return con.SqlBuilder(
            $"SELECT * FROM AdministrativeDivisions WHERE Id = {id}")
            .QuerySingleOrDefault<AdministrativeDivision>();
    }

    public Guid Create(AdministrativeDivision division) {
        using var con = _context.GetConnection();
        con.SqlBuilder(
            $"INSERT INTO AdministrativeDivisions (Id, ParentId, Name, Depth, AdminCode, PostCode) " +
            $"VALUES ({division.Id}, {division.ParentId}, {division.Name}, {division.Depth}, " +
            $"{division.AdminCode}, {division.PostCode})")
            .Execute();

        return division.Id;
    }

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