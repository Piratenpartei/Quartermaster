using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class AdminDivisionImportServiceTests : RepositoryTestBase {
    private DbContext _context = default!;
    private IServiceProvider _serviceProvider = default!;
    private AdminDivisionImportService _service = default!;
    private string _tempDir = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _serviceProvider = Worker.CreateServiceProvider();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var searchIndex = new AdministrativeDivisionSearchIndex(scopeFactory, NullLogger<AdministrativeDivisionSearchIndex>.Instance);
        _service = new AdminDivisionImportService(scopeFactory, searchIndex, NullLogger<AdminDivisionImportService>.Instance);

        _tempDir = Path.Combine(Path.GetTempPath(), "qm_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public override void Dispose() {
        base.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteFiles(string[] baseLines, string[] postcodeLines) {
        File.WriteAllText(Path.Combine(_tempDir, "DE_Base.txt"), string.Join("\n", baseLines));
        File.WriteAllText(Path.Combine(_tempDir, "DE_PostCodes.txt"), string.Join("\n", postcodeLines));
    }

    // Geonames format: geonameid, name, asciiname, alternatenames, lat, lon, feature_class, feature_code, country_code, cc2, admin1, admin2, admin3, admin4
    private static string BaseLine(int id, string name, string altNames, string featureCode,
        int admin1, int admin2 = 0, int admin3 = 0, int admin4 = 0)
        => $"{id}\t{name}\t{name}\t{altNames}\t0\t0\tA\t{featureCode}\tDE\t\t{admin1}\t{admin2}\t{admin3}\t{admin4}";

    // Postcode format: country, postcode, name, state, ..., ..., ..., ..., admin3
    private static string PostcodeLine(string postcode, string name, int admin3)
        => $"DE\t{postcode}\t{name}\t\t\t\t\t\t{admin3}";

    /// <summary>
    /// Initial state: Niedersachsen hierarchy + Bayern hierarchy (for orphan testing).
    /// Divisions: World(auto), DE(auto), ADM1:3, ADM2:254, ADM3:3254, ADM4:3254001, ADM4:3254002,
    ///            ADM3:3255, ADM3:3258(no postcodes), ADM1:9, ADM2:47, ADM3:9471
    /// </summary>
    private void WriteInitialFiles() {
        WriteFiles(
            [
                BaseLine(1, "Niedersachsen", "Land Niedersachsen", "ADM1", 3),
                BaseLine(2, "Region Hannover", "", "ADM2", 3, 254),
                BaseLine(3, "Landkreis Hildesheim", "", "ADM3", 3, 254, 3254),
                BaseLine(4, "Sibbesse", "", "ADM4", 3, 254, 3254, 3254001),
                BaseLine(5, "Alfeld", "", "ADM4", 3, 254, 3254, 3254002),
                BaseLine(6, "Wedemark", "", "ADM3", 3, 254, 3255),
                BaseLine(7, "EmptyCounty", "", "ADM3", 3, 254, 3258),
                BaseLine(8, "Bayern", "", "ADM1", 9),
                BaseLine(9, "Oberfranken", "", "ADM2", 9, 47),
                BaseLine(10, "OrphanCounty", "", "ADM3", 9, 47, 9471),
            ],
            [
                PostcodeLine("31079", "Sibbesse", 3254),
                PostcodeLine("31061", "Alfeld", 3254),
                PostcodeLine("30900", "Wedemark", 3255),
                PostcodeLine("95000", "OrphanCounty", 9471),
            ]);
    }

    /// <summary>
    /// Changed state:
    /// - ADM3:3254 name changed (Landkreis → Kreis Hildesheim)
    /// - ADM4:3254002 (Alfeld) removed → remaps to parent 3254
    /// - ADM3:3255 (Wedemark) removed → postcode 30900 remaps to new ADM3:3257
    /// - ADM3:3258 (EmptyCounty) removed → no postcodes, parent 254 exists → parent remap
    /// - ADM3:3257 (Goslar) added
    /// - ADM1:9, ADM2:47, ADM3:9471 all removed → 47 and 9471 become orphans
    /// </summary>
    private void WriteChangedFiles() {
        WriteFiles(
            [
                BaseLine(1, "Niedersachsen", "Land Niedersachsen", "ADM1", 3),
                BaseLine(2, "Region Hannover", "", "ADM2", 3, 254),
                BaseLine(3, "Kreis Hildesheim", "", "ADM3", 3, 254, 3254),
                BaseLine(4, "Sibbesse", "", "ADM4", 3, 254, 3254, 3254001),
                BaseLine(10, "Goslar", "", "ADM3", 3, 254, 3257),
            ],
            [
                PostcodeLine("31079", "Sibbesse", 3254),
                PostcodeLine("30900", "Goslar", 3257),
            ]);
    }

    private void WriteAlternateNames(params string[] lines) {
        File.WriteAllText(Path.Combine(_tempDir, AdminDivisionImportService.AlternateNamesFileName), string.Join("\n", lines));
    }

    // GeoNames alternate names format: alternateNameId, geonameid, isolanguage, name, isPreferredName, isShortName, isColloquial, isHistoric, from, to
    private static string AlternateLine(int geoNameId, string language, string name, bool preferred = false) {
        return $"0\t{geoNameId}\t{language}\t{name}\t{(preferred ? "1" : "")}\t\t\t\t\t";
    }

    private void WriteGv100Ad(params string[] lines) {
        File.WriteAllText(Path.Combine(_tempDir, "GV100AD.txt"), string.Join("\r\n", lines));
    }

    // GV100AD record: type, date, region (Land+RB+Kreis), Gemeinde (blank on type 50), Gemeindeverband, name (50), seat (50), Textkennzeichen.
    private static string Gv100Line(string type, string region, string municipality, string association, string name, int kind)
        => $"{type}20260930{region}{municipality,3}{association}{name,-50}{"",-50}{kind:00}".PadRight(220);

    [Test]
    public async Task Import_links_municipalities_to_their_GV100AD_association() {
        WriteInitialFiles();
        WriteGv100Ad(
            Gv100Line("50", "03254", "", "5401", "Sibbesse", 52),
            Gv100Line("60", "03254", "001", "5401", "Sibbesse", 64));

        var log = _service.Import(_tempDir);

        var sibbesse = _context.AdministrativeDivisions.Single(d => d.AdminCode == "3254001");
        await Assert.That(log.ErrorCount).IsEqualTo(0);
        await Assert.That(sibbesse.MunicipalityAssociationKey).IsEqualTo("032545401");
        await Assert.That(_context.MunicipalityAssociations.Single(a => a.Key == "032545401").Kind).IsEqualTo(52);
        await Assert.That(log.Errors).Contains("GV100AD: 1 Gemeinden ohne Zuordnung");
    }

    [Test]
    public async Task Import_shows_the_preferred_German_name_and_stores_the_others() {
        WriteInitialFiles();
        WriteAlternateNames(
            AlternateLine(6, "de", "Gemeinde Wedemark", preferred: true),
            AlternateLine(6, "hsb", "Wedemarkojce"),
            AlternateLine(6, "link", "https://de.wikipedia.org/wiki/Wedemark"),
            AlternateLine(6, "ru", "Ведемарк"),
            AlternateLine(1, "de", "Land Niedersachsen", preferred: true));

        var log = _service.Import(_tempDir);

        var wedemark = _context.AdministrativeDivisions.Single(d => d.AdminCode == "3255");
        var names = _context.AdministrativeDivisionNames
            .Where(n => n.AdministrativeDivisionId == wedemark.Id)
            .Select(n => n.Name)
            .ToList();
        await Assert.That(log.ErrorCount).IsEqualTo(0);
        await Assert.That(wedemark.Name).IsEqualTo("Gemeinde Wedemark");
        await Assert.That(names).Contains("Wedemark");
        await Assert.That(names).Contains("Wedemarkojce");
        await Assert.That(names.Count).IsEqualTo(3);
        await Assert.That(_context.AdministrativeDivisions.Single(d => d.AdminCode == "3").Name).IsEqualTo("Niedersachsen");
    }

    [Test]
    public async Task Changed_alternate_names_replace_the_stored_ones() {
        WriteInitialFiles();
        WriteAlternateNames(AlternateLine(6, "hsb", "Wedemarkojce"));
        _service.Import(_tempDir);
        WriteAlternateNames(AlternateLine(6, "nds", "Wedemaark"));

        _service.Import(_tempDir);

        var names = _context.AdministrativeDivisionNames.Select(n => n.Name).ToList();
        await Assert.That(names).Contains("Wedemaark");
        await Assert.That(names).DoesNotContain("Wedemarkojce");
    }

    [Test]
    public async Task Import_without_GV100AD_file_still_imports_divisions() {
        WriteInitialFiles();

        var log = _service.Import(_tempDir);

        await Assert.That(log.ErrorCount).IsEqualTo(0);
        await Assert.That(_context.AdministrativeDivisions.Any(d => d.AdminCode == "3254001")).IsTrue();
        await Assert.That(_context.MunicipalityAssociations.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Changed_GV100AD_file_triggers_a_new_import() {
        WriteInitialFiles();
        _service.Import(_tempDir);
        WriteGv100Ad(Gv100Line("50", "03254", "", "5401", "Sibbesse", 52), Gv100Line("60", "03254", "001", "5401", "Sibbesse", 64));

        _service.Import(_tempDir);

        await Assert.That(_context.AdministrativeDivisions.Single(d => d.AdminCode == "3254001").MunicipalityAssociationKey).IsEqualTo("032545401");
    }

    [Test]
    public async Task Import_FilesNotFound_ReturnsErrorLog() {
        var log = _service.Import(_tempDir);

        await Assert.That(log.ErrorCount).IsEqualTo(1);
        await Assert.That(log.Errors).IsNotNull();
        await Assert.That(log.AddedRecords).IsEqualTo(0);
    }

    [Test]
    public async Task Import_SameFileHash_SkipsImport() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        var log = _service.Import(_tempDir);

        await Assert.That(log.TotalRecords).IsEqualTo(0);
        await Assert.That(log.AddedRecords).IsEqualTo(0);
    }

    [Test]
    public async Task Import_SetsHasCompletedInitialLoad() {
        WriteInitialFiles();

        await Assert.That(_service.HasCompletedInitialLoad).IsFalse();

        _service.Import(_tempDir);

        await Assert.That(_service.HasCompletedInitialLoad).IsTrue();
    }

    [Test]
    public async Task Import_EmptyDatabase_BulkInsertsAllDivisions() {
        WriteInitialFiles();

        var log = _service.Import(_tempDir);

        await Assert.That(log.AddedRecords).IsGreaterThan(0);
        await Assert.That(log.UpdatedRecords).IsEqualTo(0);
        await Assert.That(log.RemovedRecords).IsEqualTo(0);

        var hildesheim = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3254").FirstOrDefault();
        await Assert.That(hildesheim).IsNotNull();
        await Assert.That(hildesheim!.Name).IsEqualTo("Landkreis Hildesheim");

        var sibbesse = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3254001").FirstOrDefault();
        await Assert.That(sibbesse).IsNotNull();
        await Assert.That(sibbesse!.Name).IsEqualTo("Sibbesse");
    }

    [Test]
    public async Task Import_EmptyDatabase_PersistsLogToDatabase() {
        WriteInitialFiles();

        _service.Import(_tempDir);

        var logs = _context.AdminDivisionImportLogs.ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].AddedRecords).IsGreaterThan(0);
        await Assert.That(logs[0].FileHash).IsNotNull();
        await Assert.That(logs[0].DurationMs).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Import_NameChanged_UpdatesDivision() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        WriteChangedFiles();
        _service.Import(_tempDir);

        var hildesheim = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3254").First();
        await Assert.That(hildesheim.Name).IsEqualTo("Kreis Hildesheim");
    }

    [Test]
    public async Task Import_NewDivision_AddedToDatabase() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        WriteChangedFiles();
        _service.Import(_tempDir);

        var goslar = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3257").FirstOrDefault();
        await Assert.That(goslar).IsNotNull();
        await Assert.That(goslar!.Name).IsEqualTo("Goslar");
    }

    [Test]
    public async Task Import_RemovedDivision_RemappedByPostcode() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        WriteChangedFiles();
        var log = _service.Import(_tempDir);

        // Wedemark (3255) removed, postcode 30900 maps to Goslar (3257)
        var wedemark = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3255").FirstOrDefault();
        await Assert.That(wedemark).IsNull();

        var goslar = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3257").FirstOrDefault();
        await Assert.That(goslar).IsNotNull();

        await Assert.That(log.RemappedRecords).IsGreaterThan(0);
    }

    [Test]
    public async Task Import_RemovedDivision_RemappedByParent() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        // EmptyCounty (3258) has no postcodes, parent 254 (Region Hannover) still in new data
        WriteChangedFiles();
        _service.Import(_tempDir);

        var emptyCounty = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3258").FirstOrDefault();
        await Assert.That(emptyCounty).IsNull();
    }

    [Test]
    public async Task Import_RemovedDivision_OrphanedWhenNoReplacement() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        // OrphanCounty (9471): postcode 95000 not in new data, parent 47 also removed
        WriteChangedFiles();
        var log = _service.Import(_tempDir);

        // Orphaned divisions are kept in DB
        var orphan = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "9471").FirstOrDefault();
        await Assert.That(orphan).IsNotNull();

        await Assert.That(log.OrphanedRecords).IsGreaterThan(0);
        await Assert.That(log.ErrorCount).IsEqualTo(0);
        await Assert.That(log.Errors).Contains("ORPHANED division");
    }

    [Test]
    public async Task Import_RemappedDivision_UpdatesMemberReferences() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        var wedemark = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3255").First();
        var memberId = Guid.NewGuid();
        _context.Insert(new Member {
            Id = memberId,
            MemberNumber = 1001,
            FirstName = "Test",
            LastName = "User",
            ResidenceAdministrativeDivisionId = wedemark.Id
        });

        WriteChangedFiles();
        _service.Import(_tempDir);

        var member = _context.Members.Where(m => m.Id == memberId).First();
        var goslar = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3257").First();
        await Assert.That(member.ResidenceAdministrativeDivisionId).IsEqualTo(goslar.Id);
    }

    [Test]
    public async Task Import_RemappedDivision_MovesSupportSignatureCounts() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        // Wedemark (3255) merges into Goslar (3257), which is added by the same import
        var wedemark = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3255").First();
        WriteChangedFiles();
        var goslarId = Guid.NewGuid();
        _context.Insert(new AdministrativeDivision { Id = goslarId, Name = "Goslar", AdminCode = "3257", Depth = wedemark.Depth, ParentId = wedemark.ParentId });

        var chapterId = Guid.NewGuid();
        _context.Insert(new Chapter { Id = chapterId, Name = "Test Chapter" });
        var campaign = new SignatureCampaign { ChapterId = chapterId, Name = "Liste", Target = 10, Deadline = DateTime.UtcNow.AddDays(30) };
        _context.Insert(campaign);
        var intakeId = Guid.NewGuid();
        _context.Insert(new SignatureIntake { Id = intakeId, CampaignId = campaign.Id, AdministrativeDivisionId = wedemark.Id, Count = 3, ReceivedOn = DateTime.UtcNow.Date });
        var wedemarkOffice = new CertificationOffice { Name = "Gemeinde Wedemark" };
        var goslarOffice = new CertificationOffice { Name = "Stadt Goslar" };
        _context.Insert(wedemarkOffice);
        _context.Insert(goslarOffice);
        _context.Insert(new CertificationOfficeMunicipality { OfficeId = wedemarkOffice.Id, AdministrativeDivisionId = wedemark.Id });
        _context.Insert(new CertificationOfficeMunicipality { OfficeId = goslarOffice.Id, AdministrativeDivisionId = goslarId });
        var shipment = new SignatureShipment { CampaignId = campaign.Id, OfficeId = goslarOffice.Id, SentOn = DateTime.UtcNow.Date };
        _context.Insert(shipment);
        _context.Insert(new SignatureShipmentLine { ShipmentId = shipment.Id, AdministrativeDivisionId = wedemark.Id, Count = 3 });
        _context.Insert(new SignatureShipmentLine { ShipmentId = shipment.Id, AdministrativeDivisionId = goslarId, Count = 2 });
        var signatureReturn = new SignatureReturn { ShipmentId = shipment.Id, ReturnedOn = DateTime.UtcNow.Date };
        _context.Insert(signatureReturn);
        _context.Insert(new SignatureReturnLine { ReturnId = signatureReturn.Id, AdministrativeDivisionId = wedemark.Id, Certified = 1 });

        var log = _service.Import(_tempDir);

        await Assert.That(log.ErrorCount).IsEqualTo(0);
        await Assert.That(_context.AdministrativeDivisions.Any(d => d.Id == wedemark.Id)).IsFalse();
        var intake = _context.SignatureIntakes.First(i => i.Id == intakeId);
        await Assert.That(intake.AdministrativeDivisionId).IsEqualTo(goslarId);
        var officeLinks = _context.CertificationOfficeMunicipalities.Where(m => m.OfficeId == wedemarkOffice.Id || m.OfficeId == goslarOffice.Id).ToList();
        await Assert.That(officeLinks.Count).IsEqualTo(1);
        await Assert.That(officeLinks[0].OfficeId).IsEqualTo(goslarOffice.Id);
        var lines = _context.SignatureShipmentLines.Where(l => l.ShipmentId == shipment.Id).ToList();
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(lines[0].AdministrativeDivisionId).IsEqualTo(goslarId);
        await Assert.That(lines[0].Count).IsEqualTo(5);
        var returnLine = _context.SignatureReturnLines.First(l => l.ReturnId == signatureReturn.Id);
        await Assert.That(returnLine.AdministrativeDivisionId).IsEqualTo(goslarId);
    }

    [Test]
    public async Task Import_RemappedDivision_UpdatesChapterReferences() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        var wedemark = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3255").First();
        var chapterId = Guid.NewGuid();
        _context.Insert(new Chapter { Id = chapterId, Name = "Test Chapter" });
        _context.Insert(new ChapterAdministrativeDivision {
            ChapterId = chapterId,
            AdministrativeDivisionId = wedemark.Id
        });

        WriteChangedFiles();
        _service.Import(_tempDir);

        var goslar = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3257").First();
        var divisions = _context.ChapterAdministrativeDivisions
            .Where(c => c.ChapterId == chapterId)
            .Select(c => c.AdministrativeDivisionId)
            .ToList();
        await Assert.That(divisions).Contains(goslar.Id);
    }

    [Test]
    public async Task Import_ChangeDetection_LogHasCorrectStatistics() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        WriteChangedFiles();
        var log = _service.Import(_tempDir);

        await Assert.That(log.AddedRecords).IsEqualTo(1);
        await Assert.That(log.UpdatedRecords).IsEqualTo(1);
        await Assert.That(log.RemovedRecords).IsEqualTo(6);
        await Assert.That(log.RemappedRecords).IsEqualTo(4);
        await Assert.That(log.OrphanedRecords).IsEqualTo(2);
        await Assert.That(log.TotalRecords).IsGreaterThan(0);
        await Assert.That(log.DurationMs).IsGreaterThanOrEqualTo(0);
    }
}
