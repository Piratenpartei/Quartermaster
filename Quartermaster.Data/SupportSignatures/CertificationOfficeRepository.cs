using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;

namespace Quartermaster.Data.SupportSignatures;

public class CertificationOfficeRepository {
    private readonly DbContext _context;

    public CertificationOfficeRepository(DbContext context) {
        _context = context;
    }

    public DataConnectionTransaction BeginTransaction() {
        return _context.BeginTransaction();
    }

    public CertificationOffice? Get(Guid id) {
        return _context.CertificationOffices.FirstOrDefault(o => o.Id == id);
    }

    public List<CertificationOffice> List() {
        return _context.CertificationOffices
            .OrderBy(o => o.Name)
            .ToList();
    }

    public List<CertificationOffice> ListCustom() {
        return _context.CertificationOffices
            .Where(o => o.OfficialKey == null)
            .OrderBy(o => o.Name)
            .ToList();
    }

    public List<CertificationOffice> GetMany(IEnumerable<Guid> ids) {
        var list = ids.Distinct().ToList();
        return _context.CertificationOffices
            .Where(o => list.Contains(o.Id))
            .ToList();
    }

    /// <summary>Hand-entered municipality ids per office.</summary>
    public Dictionary<Guid, List<Guid>> GetMunicipalityIdsByOffice() {
        return _context.CertificationOfficeMunicipalities
            .ToList()
            .GroupBy(m => m.OfficeId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.AdministrativeDivisionId).ToList());
    }

    public List<Guid> GetMunicipalityIds(Guid officeId) {
        return _context.CertificationOfficeMunicipalities
            .Where(m => m.OfficeId == officeId)
            .Select(m => m.AdministrativeDivisionId)
            .ToList();
    }

    public List<Guid> GetOfficialMunicipalityIds(Guid officeId) {
        return _context.OfficialOfficeMunicipalities
            .Where(m => m.OfficeId == officeId)
            .Select(m => m.AdministrativeDivisionId)
            .ToList();
    }

    public Guid? GetCustomOfficeIdByMunicipality(Guid municipalityId) {
        return _context.CertificationOfficeMunicipalities
            .Where(m => m.AdministrativeDivisionId == municipalityId)
            .Select(m => (Guid?)m.OfficeId)
            .FirstOrDefault();
    }

    public Guid? GetOfficialOfficeIdByMunicipality(Guid municipalityId) {
        return _context.OfficialOfficeMunicipalities
            .Where(m => m.AdministrativeDivisionId == municipalityId)
            .Select(m => (Guid?)m.OfficeId)
            .FirstOrDefault();
    }

    /// <summary>The hand-entered office of each given municipality that has one.</summary>
    public Dictionary<Guid, Guid> GetCustomOfficeIdsByMunicipality(IEnumerable<Guid> municipalityIds) {
        var ids = municipalityIds.Distinct().ToList();
        return _context.CertificationOfficeMunicipalities
            .Where(m => ids.Contains(m.AdministrativeDivisionId))
            .ToDictionary(m => m.AdministrativeDivisionId, m => m.OfficeId);
    }

    public Dictionary<Guid, Guid> GetOfficialOfficeIdsByMunicipality(IEnumerable<Guid> municipalityIds) {
        var ids = municipalityIds.Distinct().ToList();
        return _context.OfficialOfficeMunicipalities
            .Where(m => ids.Contains(m.AdministrativeDivisionId))
            .ToDictionary(m => m.AdministrativeDivisionId, m => m.OfficeId);
    }

    /// <summary>The office forms of each given municipality go to: the hand-entered one, else the one of the official directory.</summary>
    public Dictionary<Guid, Guid> GetOfficeIdsByMunicipality(IEnumerable<Guid> municipalityIds) {
        var ids = municipalityIds.Distinct().ToList();
        var offices = GetOfficialOfficeIdsByMunicipality(ids);
        foreach (var (municipalityId, officeId) in GetCustomOfficeIdsByMunicipality(ids)) {
            offices[municipalityId] = officeId;
        }
        return offices;
    }

    /// <summary>The given municipalities that already belong to an office other than <paramref name="exceptOfficeId"/>.</summary>
    public List<Guid> FindTakenMunicipalities(IEnumerable<Guid> municipalityIds, Guid? exceptOfficeId) {
        var ids = municipalityIds.Distinct().ToList();
        return _context.CertificationOfficeMunicipalities
            .Where(m => ids.Contains(m.AdministrativeDivisionId) && (exceptOfficeId == null || m.OfficeId != exceptOfficeId))
            .Select(m => m.AdministrativeDivisionId)
            .ToList();
    }

    public HashSet<Guid> GetOfficeIdsWithShipments() {
        return _context.SignatureShipments
            .Select(s => s.OfficeId)
            .Distinct()
            .ToList()
            .ToHashSet();
    }

    public bool HasShipments(Guid officeId) {
        return _context.SignatureShipments.Any(s => s.OfficeId == officeId);
    }

    public void Create(CertificationOffice office, IEnumerable<Guid> municipalityIds) {
        _context.Insert(office);
        InsertMunicipalities(office.Id, municipalityIds);
    }

    public void Update(CertificationOffice office, IEnumerable<Guid> municipalityIds) {
        _context.CertificationOffices
            .Where(o => o.Id == office.Id)
            .Set(o => o.Name, office.Name)
            .Set(o => o.AddressAddition, office.AddressAddition)
            .Set(o => o.Street, office.Street)
            .Set(o => o.PostCode, office.PostCode)
            .Set(o => o.City, office.City)
            .Set(o => o.Email, office.Email)
            .Set(o => o.Phone, office.Phone)
            .Set(o => o.Notes, office.Notes)
            .Update();
        _context.CertificationOfficeMunicipalities
            .Where(m => m.OfficeId == office.Id)
            .Delete();
        InsertMunicipalities(office.Id, municipalityIds);
    }

    public void Delete(Guid officeId) {
        _context.CertificationOfficeMunicipalities
            .Where(m => m.OfficeId == officeId)
            .Delete();
        _context.CertificationOffices
            .Where(o => o.Id == officeId)
            .Delete();
    }

    /// <summary>Moves the municipalities to the office, away from any other hand-entered office.</summary>
    public void Assign(Guid officeId, IEnumerable<Guid> municipalityIds) {
        var ids = municipalityIds.Distinct().ToList();
        RemoveAssignments(ids);
        InsertMunicipalities(officeId, ids);
    }

    /// <summary>Drops the hand-entered office of the municipalities, so the official directory applies again.</summary>
    public int RemoveAssignments(IEnumerable<Guid> municipalityIds) {
        var ids = municipalityIds.Distinct().ToList();
        return _context.CertificationOfficeMunicipalities
            .Where(m => ids.Contains(m.AdministrativeDivisionId))
            .Delete();
    }

    private void InsertMunicipalities(Guid officeId, IEnumerable<Guid> municipalityIds) {
        foreach (var municipalityId in municipalityIds.Distinct()) {
            _context.Insert(new CertificationOfficeMunicipality { OfficeId = officeId, AdministrativeDivisionId = municipalityId });
        }
    }
}
