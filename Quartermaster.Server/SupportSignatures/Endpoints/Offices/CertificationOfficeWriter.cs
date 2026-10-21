using System;
using System.Linq;
using MySqlConnector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

/// <summary>Creates and updates offices for the create and update endpoints; returns the error key when the municipalities do not fit.</summary>
public class CertificationOfficeWriter {
    private readonly CertificationOfficeRepository _offices;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly AuditLogRepository _auditLog;

    public CertificationOfficeWriter(CertificationOfficeRepository offices, AdministrativeDivisionRepository divisions, AuditLogRepository auditLog) {
        _offices = offices;
        _divisions = divisions;
        _auditLog = auditLog;
    }

    public (CertificationOffice? Office, int Status, string? ErrorKey) Save(CertificationOfficeSaveRequest request, Guid? existingId) {
        var municipalityIds = request.MunicipalityIds.Distinct().ToList();
        var divisions = _divisions.GetMany(municipalityIds);
        var allMunicipalities = divisions.Count == municipalityIds.Count
            && divisions.All(d => d.Depth == AdministrativeDivisionRepository.MunicipalityDepth);
        if (!allMunicipalities)
            return (null, 400, I18nKey.Error.SupportSignatures.Office.NotAMunicipality);

        using var tx = _offices.BeginTransaction();
        if (_offices.FindTakenMunicipalities(municipalityIds, existingId).Count > 0)
            return (null, 409, I18nKey.Error.SupportSignatures.Office.MunicipalityTaken);

        var office = new CertificationOffice {
            Id = existingId ?? Guid.NewGuid(),
            Name = request.Name.Trim(),
            Street = request.Street.Trim(),
            PostCode = request.PostCode.Trim(),
            City = request.City.Trim(),
            Email = Clean(request.Email),
            Phone = Clean(request.Phone),
            Notes = Clean(request.Notes)
        };
        try {
            if (existingId == null) {
                _offices.Create(office, municipalityIds);
                _auditLog.LogCreated("CertificationOffice", office.Id);
            } else {
                _offices.Update(office, municipalityIds);
                _auditLog.Log("CertificationOffice", office.Id, "Updated");
            }

            tx.Commit();
        } catch (MySqlException ex) when (IsTakenMeanwhile(ex)) {
            return (null, 409, I18nKey.Error.SupportSignatures.Office.MunicipalityTaken);
        }

        return (office, 200, null);
    }

    /// <summary>Another office took one of the municipalities after our check; MariaDB reports that as a duplicate or, with snapshot isolation, as a changed record.</summary>
    private static bool IsTakenMeanwhile(MySqlException ex) {
        return ex.ErrorCode is MySqlErrorCode.DuplicateKeyEntry or MySqlErrorCode.CheckRead or MySqlErrorCode.LockDeadlock;
    }

    private static string? Clean(string? value) {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
