using System.Threading.Channels;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Email;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.Email;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Email;

public class EmailServiceTests : IDisposable {
    private DbContext _context = default!;
    private EmailService _service = default!;
    private Channel<EmailMessage> _channel = default!;
    private OptionRepository _optionRepo = default!;
    private ChapterRepository _chapterRepo = default!;

    private Guid _chapterId;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        var auditLog = new AuditLogRepository(_context);
        var emailLogRepo = new EmailLogRepository(_context);
        _optionRepo = new OptionRepository(_context, auditLog);
        var memberRepo = new MemberRepository(_context, auditLog);
        _chapterRepo = new ChapterRepository(_context);
        var adminDivRepo = new AdministrativeDivisionRepository(_context);
        _channel = Channel.CreateUnbounded<EmailMessage>();

        _service = new EmailService(
            emailLogRepo, _optionRepo, memberRepo, _chapterRepo, adminDivRepo,
            _channel, NullLogger<EmailService>.Instance);

        // Seed chapter
        _chapterId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = _chapterId,
            Name = "Test Chapter",
            ShortCode = "tst",
            ExternalCode = "TST"
        });

        // Seed option definition for template
        _context.Insert(new OptionDefinition {
            Identifier = "test.template",
            FriendlyName = "Test Template Subject",
            DataType = OptionDataType.Template,
            IsOverridable = true
        });
    }

    [Test]
    public async Task SendEmail_TemplateOverrideUsed() {
        var (count, error) = _service.SendEmail(
            "ManualAddresses", Guid.Empty, "test.template",
            "Override content here", "recipient@example.com");

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(error).IsNull();

        _channel.Writer.Complete();
        var messages = new List<EmailMessage>();
        await foreach (var msg in _channel.Reader.ReadAllAsync())
            messages.Add(msg);

        await Assert.That(messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SendEmail_NoTemplateContent_ReturnsError() {
        var (count, error) = _service.SendEmail(
            "ManualAddresses", Guid.Empty, "nonexistent.template",
            null, "recipient@example.com");

        await Assert.That(count).IsEqualTo(0);
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task SendEmail_ManualAddresses_SplitValidatedDeduplicated() {
        var (count, error) = _service.SendEmail(
            "ManualAddresses", Guid.Empty, "",
            "Test content", "a@test.com, b@test.com, a@test.com");

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_ManualAddresses_InvalidEmailsSkipped() {
        var (count, error) = _service.SendEmail(
            "ManualAddresses", Guid.Empty, "",
            "Test content", "valid@test.com, not-an-email, also-invalid");

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_ChapterTarget_EnqueuesEmailsForMembers() {
        // Seed members in chapter
        _context.Insert(new Member {
            MemberNumber = 5001,
            FirstName = "Alice",
            LastName = "A",
            EMail = "alice@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 5002,
            FirstName = "Bob",
            LastName = "B",
            EMail = "bob@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });

        _optionRepo.SetValue("test.template", null, "Hello **{{ member.FirstName }}**");

        var (count, error) = _service.SendEmail(
            "Chapter", _chapterId, "test.template",
            null, null);

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(error).IsNull();

        _channel.Writer.Complete();
        var messages = new List<EmailMessage>();
        await foreach (var msg in _channel.Reader.ReadAllAsync())
            messages.Add(msg);

        await Assert.That(messages.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SendEmail_MembersWithoutEmail_Skipped() {
        _context.Insert(new Member {
            MemberNumber = 6001,
            FirstName = "NoEmail",
            LastName = "Person",
            EMail = null,
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 6002,
            FirstName = "HasEmail",
            LastName = "Person",
            EMail = "has@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });

        _optionRepo.SetValue("test.template", null, "Content");

        var (count, error) = _service.SendEmail(
            "Chapter", _chapterId, "test.template",
            null, null);

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task SendEmail_ReturnsCorrectCount() {
        _context.Insert(new Member {
            MemberNumber = 7001,
            FirstName = "One",
            LastName = "Member",
            EMail = "one@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 7002,
            FirstName = "Two",
            LastName = "Member",
            EMail = "two@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 7003,
            FirstName = "Three",
            LastName = "Member",
            EMail = "three@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });

        _optionRepo.SetValue("test.template", null, "Content");

        var (count, error) = _service.SendEmail(
            "Chapter", _chapterId, "test.template",
            null, null);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_AdminDivisionTarget_IncludesDescendants() {
        // Seed 3-level hierarchy: State → County → City
        var stateId = Guid.NewGuid();
        var countyId = Guid.NewGuid();
        var cityId = Guid.NewGuid();

        _context.Insert(new AdministrativeDivision {
            Id = stateId, Name = "State", Depth = 4, AdminCode = "ST"
        });
        _context.Insert(new AdministrativeDivision {
            Id = countyId, Name = "County", Depth = 5, AdminCode = "CNT", ParentId = stateId
        });
        _context.Insert(new AdministrativeDivision {
            Id = cityId, Name = "City", Depth = 6, AdminCode = "CITY", ParentId = countyId
        });

        // Seed members at different levels
        _context.Insert(new Member {
            MemberNumber = 9001, FirstName = "StateResident", LastName = "S",
            EMail = "state@test.com",
            ResidenceAdministrativeDivisionId = stateId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 9002, FirstName = "CountyResident", LastName = "C",
            EMail = "county@test.com",
            ResidenceAdministrativeDivisionId = countyId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 9003, FirstName = "CityResident", LastName = "Ci",
            EMail = "city@test.com",
            ResidenceAdministrativeDivisionId = cityId,
            LastImportedAt = DateTime.UtcNow
        });

        _optionRepo.SetValue("test.template", null, "Hello");

        // Targeting the state should reach all 3 (state + descendants)
        var (count, error) = _service.SendEmail(
            "AdministrativeDivision", stateId, "test.template",
            null, null);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_AdminDivisionTarget_LeafReachesOnlyLeafMembers() {
        // Same hierarchy but target the leaf (city)
        var stateId = Guid.NewGuid();
        var cityId = Guid.NewGuid();

        _context.Insert(new AdministrativeDivision {
            Id = stateId, Name = "State", Depth = 4, AdminCode = "ST2"
        });
        _context.Insert(new AdministrativeDivision {
            Id = cityId, Name = "City", Depth = 6, AdminCode = "CITY2", ParentId = stateId
        });

        _context.Insert(new Member {
            MemberNumber = 9101, FirstName = "StateResident", LastName = "S",
            EMail = "state@test.com",
            ResidenceAdministrativeDivisionId = stateId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 9102, FirstName = "CityResident", LastName = "Ci",
            EMail = "city@test.com",
            ResidenceAdministrativeDivisionId = cityId,
            LastImportedAt = DateTime.UtcNow
        });

        _optionRepo.SetValue("test.template", null, "Hello");

        // Targeting the city should only reach the city resident (no ancestors)
        var (count, error) = _service.SendEmail(
            "AdministrativeDivision", cityId, "test.template",
            null, null);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(error).IsNull();
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
