using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Members;

[NotInParallel]
public class MemberImportServiceTests : IDisposable {
    private DbContext _context = default!;
    private IServiceProvider _serviceProvider = default!;
    private MemberImportService _service = default!;
    private string _csvPath = default!;

    private Guid _bundId;
    private Guid _lvNdsId;
    private Guid _lvBeId;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        _serviceProvider = TestDatabaseFixture.CreateServiceProvider();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _service = new MemberImportService(scopeFactory, NullLogger<MemberImportService>.Instance);

        // Seed chapter hierarchy: Bund -> LV Niedersachsen, LV Berlin
        _bundId = Guid.NewGuid();
        _lvNdsId = Guid.NewGuid();
        _lvBeId = Guid.NewGuid();

        _context.Insert(new Chapter {
            Id = _bundId,
            Name = "Bundesverband",
            ShortCode = "bund",
            ExternalCode = "BV"
        });
        _context.Insert(new Chapter {
            Id = _lvNdsId,
            Name = "LV Niedersachsen",
            ShortCode = "nds",
            ExternalCode = "NI",
            ParentChapterId = _bundId
        });
        _context.Insert(new Chapter {
            Id = _lvBeId,
            Name = "LV Berlin",
            ShortCode = "be",
            ExternalCode = "BE",
            ParentChapterId = _bundId
        });

        _csvPath = Path.Combine(
            Path.GetDirectoryName(typeof(MemberImportServiceTests).Assembly.Location)!,
            "TestData", "sample_import.csv");
    }

    [Test]
    public async Task ImportFromFile_ValidCsv_CreatesNewMembers() {
        var log = _service.ImportFromFile(_csvPath);

        await Assert.That(log.TotalRecords).IsEqualTo(3);
        await Assert.That(log.NewRecords).IsEqualTo(3);
        await Assert.That(log.UpdatedRecords).IsEqualTo(0);

        var anna = _context.Members.Where(m => m.MemberNumber == 1001).First();
        await Assert.That(anna.FirstName).IsEqualTo("Anna");
        await Assert.That(anna.LastName).IsEqualTo("Mueller");
        await Assert.That(anna.EMail).IsEqualTo("anna@example.com");
    }

    [Test]
    public async Task ImportFromFile_ExistingMember_IsUpdated() {
        // Pre-insert a member with MemberNumber 1001
        var existingId = Guid.NewGuid();
        _context.Insert(new Member {
            Id = existingId,
            MemberNumber = 1001,
            FirstName = "OldFirst",
            LastName = "OldLast",
            LastImportedAt = DateTime.UtcNow.AddDays(-1)
        });

        var log = _service.ImportFromFile(_csvPath);

        await Assert.That(log.UpdatedRecords).IsGreaterThan(0);
        var updated = _context.Members.Where(m => m.MemberNumber == 1001).First();
        await Assert.That(updated.FirstName).IsEqualTo("Anna");
        await Assert.That(updated.Id).IsEqualTo(existingId);
    }

    [Test]
    public async Task ImportFromFile_InvalidRow_LoggedAsError() {
        // Create a CSV where one row references a chapter ExternalCode that causes
        // a lookup error during ResolveChapter, and another is valid.
        // We force an error by creating a value too long for a DB column.
        var tempPath = Path.GetTempFileName();
        try {
            var header = "USER_Mitgliedsnummer;USER_refAufnahme;Name1;Name2;LieferStrasse;LieferLand;LieferPLZ;LieferOrt;Telefon;EMail;USER_LV;USER_Bezirk;USER_Kreis;USER_Beitrag;USER_redBeitrag;USER_Umfragen;USER_Aktionen;USER_Newsletter;USER_Geburtsdatum;USER_Postbounce;USER_Bundesland;USER_Eintrittsdatum;USER_Austrittsdatum;USER_Erstbeitrag;USER_Landkreis;USER_Gemeinde;USER_Staatsbuergerschaft;USER_zStimmberechtigung;USER_zoffenerbeitragtotal;USER_redBeitragEnde;USER_Schwebend";
            var row1 = "9001;REF;Valid;Person;NULL;NULL;NULL;NULL;NULL;NULL;;;;48.00;0.00;0;0;0;NULL;0;NULL;NULL;NULL;NULL;NULL;NULL;NULL;0;NULL;NULL;0";
            // Row with a street value exceeding the DB column limit (varchar 256)
            var longStreet = new string('A', 300);
            var row2 = $"9002;REF;Broken;Person;{longStreet};NULL;NULL;NULL;NULL;NULL;;;;48.00;0.00;0;0;0;NULL;0;NULL;NULL;NULL;NULL;NULL;NULL;NULL;0;NULL;NULL;0";
            File.WriteAllText(tempPath, $"{header}\n{row1}\n{row2}");

            var log = _service.ImportFromFile(tempPath);

            await Assert.That(log.TotalRecords).IsEqualTo(2);
            // First row should still have been imported successfully
            await Assert.That(log.NewRecords).IsGreaterThan(0);
            await Assert.That(log.ErrorCount).IsGreaterThan(0);
        } finally {
            File.Delete(tempPath);
        }
    }

    [Test]
    public async Task ImportFromFile_LogHasCorrectStatistics() {
        var log = _service.ImportFromFile(_csvPath);

        await Assert.That(log.FileName).IsEqualTo("sample_import.csv");
        await Assert.That(log.FileHash).IsNotNull();
        await Assert.That(log.TotalRecords).IsEqualTo(3);
        await Assert.That(log.DurationMs).IsGreaterThan(-1);
        await Assert.That(log.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task ImportFromFile_ResolvesChapter_LvOnly() {
        var log = _service.ImportFromFile(_csvPath);

        var anna = _context.Members.Where(m => m.MemberNumber == 1001).First();
        await Assert.That(anna.ChapterId).IsEqualTo(_lvNdsId);
    }

    [Test]
    public async Task ImportFromFile_ResolvesChapter_KreisWithParentChain() {
        // Add a Kreis under a Bezirk under NDS
        var bezirkId = Guid.NewGuid();
        var kreisId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = bezirkId,
            Name = "Bezirk Hannover",
            ExternalCode = "H",
            ParentChapterId = _lvNdsId
        });
        _context.Insert(new Chapter {
            Id = kreisId,
            Name = "Kreis Stadtverband",
            ExternalCode = "SH",
            ParentChapterId = bezirkId
        });

        // Create a CSV with Kreis + Bezirk + LV
        var tempPath = Path.GetTempFileName();
        try {
            var header = "USER_Mitgliedsnummer;USER_refAufnahme;Name1;Name2;LieferStrasse;LieferLand;LieferPLZ;LieferOrt;Telefon;EMail;USER_LV;USER_Bezirk;USER_Kreis;USER_Beitrag;USER_redBeitrag;USER_Umfragen;USER_Aktionen;USER_Newsletter;USER_Geburtsdatum;USER_Postbounce;USER_Bundesland;USER_Eintrittsdatum;USER_Austrittsdatum;USER_Erstbeitrag;USER_Landkreis;USER_Gemeinde;USER_Staatsbuergerschaft;USER_zStimmberechtigung;USER_zoffenerbeitragtotal;USER_redBeitragEnde;USER_Schwebend";
            var row = "8001;REF;Test;Kreis;NULL;NULL;NULL;NULL;NULL;NULL;NI;H;SH;48.00;0.00;0;0;0;NULL;0;NULL;NULL;NULL;NULL;NULL;NULL;NULL;0;NULL;NULL;0";
            File.WriteAllText(tempPath, $"{header}\n{row}");

            _service.ImportFromFile(tempPath);

            var member = _context.Members.Where(m => m.MemberNumber == 8001).First();
            await Assert.That(member.ChapterId).IsEqualTo(kreisId);
        } finally {
            File.Delete(tempPath);
        }
    }

    [Test]
    public async Task ImportFromFile_ResolvesChapter_AllEmpty_ReturnsNull() {
        var tempPath = Path.GetTempFileName();
        try {
            var header = "USER_Mitgliedsnummer;USER_refAufnahme;Name1;Name2;LieferStrasse;LieferLand;LieferPLZ;LieferOrt;Telefon;EMail;USER_LV;USER_Bezirk;USER_Kreis;USER_Beitrag;USER_redBeitrag;USER_Umfragen;USER_Aktionen;USER_Newsletter;USER_Geburtsdatum;USER_Postbounce;USER_Bundesland;USER_Eintrittsdatum;USER_Austrittsdatum;USER_Erstbeitrag;USER_Landkreis;USER_Gemeinde;USER_Staatsbuergerschaft;USER_zStimmberechtigung;USER_zoffenerbeitragtotal;USER_redBeitragEnde;USER_Schwebend";
            var row = "7001;REF;No;Chapter;NULL;NULL;NULL;NULL;NULL;NULL;;;;48.00;0.00;0;0;0;NULL;0;NULL;NULL;NULL;NULL;NULL;NULL;NULL;0;NULL;NULL;0";
            File.WriteAllText(tempPath, $"{header}\n{row}");

            _service.ImportFromFile(tempPath);

            var member = _context.Members.Where(m => m.MemberNumber == 7001).First();
            await Assert.That(member.ChapterId).IsNull();
        } finally {
            File.Delete(tempPath);
        }
    }

    [Test]
    public async Task ImportFromFile_ResolvesChapter_BezirkWithoutKreis() {
        var bezirkId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = bezirkId,
            Name = "Bezirk Hannover",
            ExternalCode = "H",
            ParentChapterId = _lvNdsId
        });

        var tempPath = Path.GetTempFileName();
        try {
            var header = "USER_Mitgliedsnummer;USER_refAufnahme;Name1;Name2;LieferStrasse;LieferLand;LieferPLZ;LieferOrt;Telefon;EMail;USER_LV;USER_Bezirk;USER_Kreis;USER_Beitrag;USER_redBeitrag;USER_Umfragen;USER_Aktionen;USER_Newsletter;USER_Geburtsdatum;USER_Postbounce;USER_Bundesland;USER_Eintrittsdatum;USER_Austrittsdatum;USER_Erstbeitrag;USER_Landkreis;USER_Gemeinde;USER_Staatsbuergerschaft;USER_zStimmberechtigung;USER_zoffenerbeitragtotal;USER_redBeitragEnde;USER_Schwebend";
            var row = "8002;REF;Test;Bezirk;NULL;NULL;NULL;NULL;NULL;NULL;NI;H;;48.00;0.00;0;0;0;NULL;0;NULL;NULL;NULL;NULL;NULL;NULL;NULL;0;NULL;NULL;0";
            File.WriteAllText(tempPath, $"{header}\n{row}");

            _service.ImportFromFile(tempPath);

            var member = _context.Members.Where(m => m.MemberNumber == 8002).First();
            await Assert.That(member.ChapterId).IsEqualTo(bezirkId);
        } finally {
            File.Delete(tempPath);
        }
    }

    [Test]
    public async Task ImportFromFile_ResolvesAdminDivision_SinglePostcodeMatch() {
        var divId = Guid.NewGuid();
        _context.Insert(new AdministrativeDivision {
            Id = divId,
            Name = "Hannover",
            Depth = 6,
            AdminCode = "3241",
            PostCodes = "30159"
        });

        var log = _service.ImportFromFile(_csvPath);

        // Anna has postcode 30159 → matches Hannover
        var anna = _context.Members.Where(m => m.MemberNumber == 1001).First();
        await Assert.That(anna.ResidenceAdministrativeDivisionId).IsEqualTo(divId);
    }

    [Test]
    public async Task ImportFromFile_ResolvesAdminDivision_MultipleMatchesWithCityDisambiguation() {
        var hannoverId = Guid.NewGuid();
        var otherDivId = Guid.NewGuid();
        _context.Insert(new AdministrativeDivision {
            Id = hannoverId,
            Name = "Hannover",
            Depth = 6,
            AdminCode = "3241",
            PostCodes = "30159"
        });
        _context.Insert(new AdministrativeDivision {
            Id = otherDivId,
            Name = "Langenhagen",
            Depth = 7,
            AdminCode = "3241001",
            PostCodes = "30159,30855"
        });

        var log = _service.ImportFromFile(_csvPath);

        // Anna has postcode 30159 and city "Hannover" → should match Hannover by city name
        var anna = _context.Members.Where(m => m.MemberNumber == 1001).First();
        await Assert.That(anna.ResidenceAdministrativeDivisionId).IsEqualTo(hannoverId);
    }

    [Test]
    public async Task ImportFromFile_ResolvesAdminDivision_MultipleMatchesNoCityPicksFirst() {
        var div1 = Guid.NewGuid();
        var div2 = Guid.NewGuid();
        _context.Insert(new AdministrativeDivision {
            Id = div1,
            Name = "Division A",
            Depth = 6,
            AdminCode = "9999",
            PostCodes = "10115"
        });
        _context.Insert(new AdministrativeDivision {
            Id = div2,
            Name = "Division B",
            Depth = 7,
            AdminCode = "9999001",
            PostCodes = "10115,10116"
        });

        // Create a CSV with postcode 10115 but no city
        var tempPath = Path.GetTempFileName();
        try {
            var header = "USER_Mitgliedsnummer;USER_refAufnahme;Name1;Name2;LieferStrasse;LieferLand;LieferPLZ;LieferOrt;Telefon;EMail;USER_LV;USER_Bezirk;USER_Kreis;USER_Beitrag;USER_redBeitrag;USER_Umfragen;USER_Aktionen;USER_Newsletter;USER_Geburtsdatum;USER_Postbounce;USER_Bundesland;USER_Eintrittsdatum;USER_Austrittsdatum;USER_Erstbeitrag;USER_Landkreis;USER_Gemeinde;USER_Staatsbuergerschaft;USER_zStimmberechtigung;USER_zoffenerbeitragtotal;USER_redBeitragEnde;USER_Schwebend";
            var row = "6001;REF;No;City;NULL;NULL;10115;NULL;NULL;NULL;;;;48.00;0.00;0;0;0;NULL;0;NULL;NULL;NULL;NULL;NULL;NULL;NULL;0;NULL;NULL;0";
            File.WriteAllText(tempPath, $"{header}\n{row}");

            _service.ImportFromFile(tempPath);

            var member = _context.Members.Where(m => m.MemberNumber == 6001).First();
            // Should be assigned to one of the matching divisions (not null)
            await Assert.That(member.ResidenceAdministrativeDivisionId).IsNotNull();
        } finally {
            File.Delete(tempPath);
        }
    }

    [Test]
    public async Task ImportFromFile_NoPostcode_AdminDivisionStaysNull() {
        // Lisa (member 1003) has no postcode in sample CSV
        _context.Insert(new AdministrativeDivision {
            Id = Guid.NewGuid(),
            Name = "Hannover",
            Depth = 6,
            AdminCode = "3241",
            PostCodes = "30159"
        });

        _service.ImportFromFile(_csvPath);

        var lisa = _context.Members.Where(m => m.MemberNumber == 1003).First();
        await Assert.That(lisa.ResidenceAdministrativeDivisionId).IsNull();
    }

    [Test]
    public async Task ImportFromFile_NoMatchingPostcode_AdminDivisionStaysNull() {
        // Seed division with different postcode
        _context.Insert(new AdministrativeDivision {
            Id = Guid.NewGuid(),
            Name = "FarAway",
            Depth = 6,
            AdminCode = "9999",
            PostCodes = "99999"
        });

        _service.ImportFromFile(_csvPath);

        // Anna has 30159, no division has that postcode
        var anna = _context.Members.Where(m => m.MemberNumber == 1001).First();
        await Assert.That(anna.ResidenceAdministrativeDivisionId).IsNull();
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
