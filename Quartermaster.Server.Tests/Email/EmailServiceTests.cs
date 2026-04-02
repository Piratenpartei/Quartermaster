using System.Threading.Channels;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Email;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.Email;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Email;

[NotInParallel]
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
        _channel = Channel.CreateUnbounded<EmailMessage>();

        _service = new EmailService(
            emailLogRepo, _optionRepo, memberRepo, _chapterRepo,
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

    public void Dispose() {
        _context?.Dispose();
    }
}
