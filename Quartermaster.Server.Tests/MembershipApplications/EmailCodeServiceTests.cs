using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.EmailVerification;
using Quartermaster.Data.Options;
using Quartermaster.Server.MembershipApplications.EmailCodes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.MembershipApplications;

public class EmailCodeServiceTests : RepositoryTestBase {
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private EmailVerificationCodeRepository _repository = default!;
    private EmailCodeService _service = default!;

    [Before(Test)]
    public void Setup() {
        _repository = new EmailVerificationCodeRepository(Db);
        _service = new EmailCodeService(_repository, new OptionRepository(Db, AuditLog));
    }

    [Test]
    public async Task Issue_returns_six_digits() {
        var result = _service.Issue("a@test.local", Now);

        await Assert.That(result.LimitReached).IsFalse();
        await Assert.That(result.Code).IsNotNull();
        await Assert.That(Regex.IsMatch(result.Code!, "^[0-9]{6}$")).IsTrue();
        var row = _repository.Get("a@test.local")!;
        await Assert.That(row.CodeHash).IsNotEqualTo(result.Code);
        await Assert.That(row.CodeSalt).IsNotEmpty();
    }

    [Test]
    public async Task Check_valid_sets_verified_and_extends() {
        var code = _service.Issue("a@test.local", Now).Code!;
        var checkedAt = Now.AddMinutes(10);

        var result = _service.Check("a@test.local", code, checkedAt);

        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Valid);
        var row = _repository.Get("a@test.local")!;
        await Assert.That(row.VerifiedAt).IsEqualTo(checkedAt);
        await Assert.That(row.ExpiresAt).IsEqualTo(checkedAt.AddMinutes(15));
    }

    [Test]
    public async Task Check_wrong_counts() {
        var code = _service.Issue("a@test.local", Now).Code!;

        var result = _service.Check("a@test.local", WrongCode(code), Now);

        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Wrong);
        await Assert.That(_repository.Get("a@test.local")!.FailedAttempts).IsEqualTo(1);
    }

    [Test]
    [Arguments("12345")]
    [Arguments("1234567")]
    [Arguments("12345a")]
    public async Task Check_malformed_counts_as_wrong(string malformed) {
        _service.Issue("a@test.local", Now);

        var result = _service.Check("a@test.local", malformed, Now);

        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Wrong);
        await Assert.That(_repository.Get("a@test.local")!.FailedAttempts).IsEqualTo(1);
    }

    [Test]
    public async Task Check_oversized_code_counts_as_wrong() {
        _service.Issue("a@test.local", Now);

        var result = _service.Check("a@test.local", new string('1', 100_000), Now);

        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Wrong);
    }

    [Test]
    public async Task Parallel_wrong_checks_never_exceed_the_cap() {
        var code = _service.Issue("a@test.local", Now).Code!;
        var wrong = WrongCode(code);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => {
            using var db = Worker.CreateDbContext();
            var service = new EmailCodeService(new EmailVerificationCodeRepository(db), new OptionRepository(db, new AuditLogRepository(db)));
            service.Check("a@test.local", wrong, Now);
        })));

        await Assert.That(_repository.Get("a@test.local")!.FailedAttempts).IsEqualTo(EmailCodeService.MaxFailedAttempts);
        await Assert.That(_service.Check("a@test.local", code, Now)).IsEqualTo(EmailCodeCheckResult.Locked);
    }

    [Test]
    public async Task Check_locked_after_five() {
        var code = _service.Issue("a@test.local", Now).Code!;
        for (var i = 0; i < EmailCodeService.MaxFailedAttempts; i++)
            _service.Check("a@test.local", WrongCode(code), Now);

        await Assert.That(_service.Check("a@test.local", code, Now)).IsEqualTo(EmailCodeCheckResult.Locked);
    }

    [Test]
    public async Task Check_expired() {
        var code = _service.Issue("a@test.local", Now).Code!;

        var result = _service.Check("a@test.local", WrongCode(code), Now.AddMinutes(16));

        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Expired);
        await Assert.That(_repository.Get("a@test.local")!.FailedAttempts).IsEqualTo(0);
    }

    [Test]
    public async Task Check_does_not_confuse_addresses_differing_in_diacritics() {
        var code = _service.Issue("anna@muller.de", Now).Code!;

        await Assert.That(_service.Check("anna@m\u00fcller.de", code, Now)).IsEqualTo(EmailCodeCheckResult.Expired);
    }

    [Test]
    public async Task Check_without_row_is_expired() {
        await Assert.That(_service.Check("none@test.local", "123456", Now)).IsEqualTo(EmailCodeCheckResult.Expired);
    }

    [Test]
    public async Task Address_is_normalized() {
        var code = _service.Issue(" Alice@Test.Local ", Now).Code!;

        await Assert.That(_service.Check("alice@test.local", code, Now)).IsEqualTo(EmailCodeCheckResult.Valid);
    }

    [Test]
    public async Task Reissue_resets_attempts() {
        var first = _service.Issue("a@test.local", Now).Code!;
        for (var i = 0; i < EmailCodeService.MaxFailedAttempts; i++)
            _service.Check("a@test.local", WrongCode(first), Now);

        var second = _service.Issue("a@test.local", Now).Code!;

        await Assert.That(_service.Check("a@test.local", second, Now)).IsEqualTo(EmailCodeCheckResult.Valid);
    }

    [Test]
    public async Task Reissue_invalidates_old_code() {
        var first = _service.Issue("a@test.local", Now).Code!;
        var second = first;
        while (second == first)
            second = _service.Issue("a@test.local", Now).Code!;

        await Assert.That(_service.Check("a@test.local", first, Now)).IsEqualTo(EmailCodeCheckResult.Wrong);
    }

    [Test]
    public async Task Send_limit_per_address() {
        for (var i = 0; i < 10; i++)
            await Assert.That(_service.Issue("a@test.local", Now.AddMinutes(i)).LimitReached).IsFalse();

        var blocked = _service.Issue("a@test.local", Now.AddMinutes(30));
        await Assert.That(blocked.LimitReached).IsTrue();
        await Assert.That(blocked.Code).IsNull();

        var later = _service.Issue("a@test.local", Now.AddMinutes(61));
        await Assert.That(later.LimitReached).IsFalse();
        await Assert.That(later.Code).IsNotNull();
    }

    [Test]
    public async Task Consume_deletes_row() {
        _service.Issue("a@test.local", Now);

        _service.Consume(" A@test.local");

        await Assert.That(_repository.Get("a@test.local")).IsNull();
    }

    [Test]
    public async Task DeleteExpiredBefore_removes_old_rows() {
        _service.Issue("old@test.local", Now.AddDays(-3));
        _service.Issue("new@test.local", Now);

        var deleted = _repository.DeleteExpiredBefore(Now.AddDays(-1));

        await Assert.That(deleted).IsEqualTo(1);
        await Assert.That(_repository.Get("old@test.local")).IsNull();
        await Assert.That(_repository.Get("new@test.local")).IsNotNull();
    }

    private static string WrongCode(string code) {
        return code == "000000" ? "000001" : "000000";
    }
}
