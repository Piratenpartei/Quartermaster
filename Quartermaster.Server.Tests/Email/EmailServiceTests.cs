using System.Threading.Channels;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Email;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Email;

public class EmailServiceTests : RepositoryTestBase {
    private DbContext _context = default!;
    private EmailService _service = default!;
    private Channel<EmailMessage> _channel = default!;
    private TemplateRepository _templateRepo = default!;
    private ChapterRepository _chapterRepo = default!;

    private Guid _chapterId;
    private Guid _templateId;
    private Guid _emptyBodyTemplateId;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        var logRepo = new NotificationLogRepository(_context);
        _templateRepo = new TemplateRepository(_context);
        var memberRepo = new MemberRepository(_context, AuditLog);
        _chapterRepo = new ChapterRepository(_context, AuditLog);
        var adminDivRepo = new AdministrativeDivisionRepository(_context);
        _channel = Channel.CreateUnbounded<EmailMessage>();
        var emailMessageChannel = new EmailMessageChannel(logRepo, _channel);

        _service = new EmailService(
            _templateRepo, memberRepo, _chapterRepo, adminDivRepo,
            emailMessageChannel, NullLogger<EmailService>.Instance);

        _chapterId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = _chapterId,
            Name = "Test Chapter",
            ShortCode = "tst",
            ExternalCode = "TST"
        });

        _templateId = Guid.NewGuid();
        _templateRepo.Create(new Template {
            Id = _templateId,
            DisplayName = "Test Template",
            Subject = "Test Subject",
            Body = "Default body"
        });

        _emptyBodyTemplateId = Guid.NewGuid();
        _templateRepo.Create(new Template {
            Id = _emptyBodyTemplateId,
            DisplayName = "Empty Body Template",
            Subject = "Subject only",
            Body = ""
        });
    }

    [Test]
    public async Task SendEmail_TemplateOverrideUsed() {
        var (count, error) = await _service.SendEmailAsync(
            "ManualAddresses", Guid.Empty, _templateId,
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
        var (count, error) = await _service.SendEmailAsync(
            "ManualAddresses", Guid.Empty, _emptyBodyTemplateId,
            null, "recipient@example.com");

        await Assert.That(count).IsEqualTo(0);
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task SendEmail_ManualAddresses_SplitValidatedDeduplicated() {
        var (count, error) = await _service.SendEmailAsync(
            "ManualAddresses", Guid.Empty, null,
            "Test content", "a@test.com, b@test.com, a@test.com");

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_ManualAddresses_InvalidEmailsSkipped() {
        var (count, error) = await _service.SendEmailAsync(
            "ManualAddresses", Guid.Empty, null,
            "Test content", "valid@test.com, not-an-email, also-invalid");

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_ChapterTarget_EnqueuesEmailsForMembers() {
        _context.Insert(new Member {
            MemberNumber = 5001,
            FirstName = "Alice",
            LastName = "A",
            Email = "alice@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 5002,
            FirstName = "Bob",
            LastName = "B",
            Email = "bob@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });

        var (count, error) = await _service.SendEmailAsync(
            "Chapter", _chapterId, _templateId,
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
            Email = null,
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 6002,
            FirstName = "HasEmail",
            LastName = "Person",
            Email = "has@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });

        var (count, error) = await _service.SendEmailAsync(
            "Chapter", _chapterId, _templateId,
            null, null);

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task SendEmail_ReturnsCorrectCount() {
        _context.Insert(new Member {
            MemberNumber = 7001,
            FirstName = "One",
            LastName = "Member",
            Email = "one@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 7002,
            FirstName = "Two",
            LastName = "Member",
            Email = "two@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 7003,
            FirstName = "Three",
            LastName = "Member",
            Email = "three@test.com",
            ChapterId = _chapterId,
            LastImportedAt = DateTime.UtcNow
        });

        var (count, error) = await _service.SendEmailAsync(
            "Chapter", _chapterId, _templateId,
            null, null);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_AdminDivisionTarget_IncludesDescendants() {
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

        _context.Insert(new Member {
            MemberNumber = 9001, FirstName = "StateResident", LastName = "S",
            Email = "state@test.com",
            ResidenceAdministrativeDivisionId = stateId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 9002, FirstName = "CountyResident", LastName = "C",
            Email = "county@test.com",
            ResidenceAdministrativeDivisionId = countyId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 9003, FirstName = "CityResident", LastName = "Ci",
            Email = "city@test.com",
            ResidenceAdministrativeDivisionId = cityId,
            LastImportedAt = DateTime.UtcNow
        });

        var (count, error) = await _service.SendEmailAsync(
            "AdministrativeDivision", stateId, _templateId,
            null, null);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task SendEmail_AdminDivisionTarget_LeafReachesOnlyLeafMembers() {
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
            Email = "state@test.com",
            ResidenceAdministrativeDivisionId = stateId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new Member {
            MemberNumber = 9102, FirstName = "CityResident", LastName = "Ci",
            Email = "city@test.com",
            ResidenceAdministrativeDivisionId = cityId,
            LastImportedAt = DateTime.UtcNow
        });

        var (count, error) = await _service.SendEmailAsync(
            "AdministrativeDivision", cityId, _templateId,
            null, null);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(error).IsNull();
    }

}
