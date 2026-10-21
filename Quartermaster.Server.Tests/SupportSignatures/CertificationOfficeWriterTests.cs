using System;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures.Endpoints.Offices;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class CertificationOfficeWriterTests : RepositoryTestBase {
    private AdministrativeDivision _ahnsbeck = default!;

    [Before(Test)]
    public void Setup() {
        _ahnsbeck = new AdministrativeDivision { Name = "Ahnsbeck", Depth = 7 };
        Db.Insert(_ahnsbeck);
    }

    private static CertificationOfficeWriter NewWriterOn(DbContext db) {
        return new CertificationOfficeWriter(new CertificationOfficeRepository(db), new AdministrativeDivisionRepository(db), new AuditLogRepository(db));
    }

    [Test]
    public async Task Office_created_meanwhile_for_the_same_municipality_is_a_conflict() {
        using var holder = Worker.CreateDbContext();
        using var dbA = Worker.CreateDbContext();
        var writerA = NewWriterOn(dbA);
        var request = new CertificationOfficeSaveRequest {
            Name = "Samtgemeinde Lachendorf", Street = "Oppershäuser Str. 1", PostCode = "29331", City = "Lachendorf", MunicipalityIds = [_ahnsbeck.Id]
        };

        (CertificationOffice? Office, int Status, string? ErrorKey) result;
        var holderOffices = new CertificationOfficeRepository(holder);
        using (var tx = holderOffices.BeginTransaction()) {
            holderOffices.Create(new CertificationOffice { Name = "Gemeinde Ahnsbeck", Street = "Dorfstr. 1", PostCode = "29353", City = "Ahnsbeck" }, [_ahnsbeck.Id]);
            var save = Task.Run(() => writerA.Save(request, null));
            await Task.Delay(500);
            tx.Commit();
            result = await save;
        }

        await Assert.That(result.Status).IsEqualTo(409);
        await Assert.That(result.ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.Office.MunicipalityTaken);
    }
}
