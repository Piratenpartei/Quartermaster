using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class AdminDivisionImportServiceTests : IDisposable {
    private DbContext _context = default!;
    private IServiceProvider _serviceProvider = default!;
    private AdminDivisionImportService _service = default!;
    private string _tempDir = default!;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        _serviceProvider = TestDatabaseFixture.CreateServiceProvider();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _service = new AdminDivisionImportService(scopeFactory, NullLogger<AdminDivisionImportService>.Instance);

        _tempDir = Path.Combine(Path.GetTempPath(), "qm_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() {
        _context?.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    #region File helpers

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

    #endregion

    #region File handling

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

    #endregion

    #region Initial load

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

    #endregion

    #region Change detection — updates

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

    #endregion

    #region Change detection — removals and remapping

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
    }

    #endregion

    #region Change detection — reference updates

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
    public async Task Import_RemappedDivision_UpdatesChapterReferences() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        var wedemark = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3255").First();
        var chapterId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = chapterId,
            Name = "Test Chapter",
            AdministrativeDivisionId = wedemark.Id
        });

        WriteChangedFiles();
        _service.Import(_tempDir);

        var chapter = _context.Chapters.Where(c => c.Id == chapterId).First();
        var goslar = _context.AdministrativeDivisions
            .Where(d => d.AdminCode == "3257").First();
        await Assert.That(chapter.AdministrativeDivisionId).IsEqualTo(goslar.Id);
    }

    #endregion

    #region Change detection — log statistics

    [Test]
    public async Task Import_ChangeDetection_LogHasCorrectStatistics() {
        WriteInitialFiles();
        _service.Import(_tempDir);

        WriteChangedFiles();
        var log = _service.Import(_tempDir);

        // 1 new division (Goslar)
        await Assert.That(log.AddedRecords).IsEqualTo(1);
        // 2 updated (3254 name+postcodes, 3254001 postcodes)
        await Assert.That(log.UpdatedRecords).IsEqualTo(2);
        // 6 removed (3254002, 3255, 3258, 9, 47, 9471)
        await Assert.That(log.RemovedRecords).IsEqualTo(6);
        // 4 remapped (3254002→3254, 3255→3257, 3258→254, 9→DE)
        await Assert.That(log.RemappedRecords).IsEqualTo(4);
        // 2 orphaned (47, 9471)
        await Assert.That(log.OrphanedRecords).IsEqualTo(2);
        await Assert.That(log.TotalRecords).IsGreaterThan(0);
        await Assert.That(log.DurationMs).IsGreaterThanOrEqualTo(0);
    }

    #endregion
}
