using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Notifications;

public class TelegramLinkTokenRepositoryTests : RepositoryTestBase {
    private TelegramLinkTokenRepository _repo = default!;
    private TestDataBuilder _builder = default!;

    [Before(Test)]
    public void Setup() {
        _repo = new TelegramLinkTokenRepository(Db);
        _builder = new TestDataBuilder(Db);
    }

    [Test]
    public async Task Create_returns_token_with_expiry_in_the_future() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var token = _repo.Create(user.Id, now);
        await Assert.That(token.Token.Length).IsGreaterThan(20);
        await Assert.That(token.UserId).IsEqualTo(user.Id);
        await Assert.That(token.ExpiresAt > now).IsTrue();
        await Assert.That(token.ConsumedAt).IsNull();
    }

    [Test]
    public async Task Consume_links_chat_id_and_marks_consumed() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var token = _repo.Create(user.Id, now);

        var linkedUserId = _repo.Consume(token.Token, "987654321", now.AddMinutes(1));

        await Assert.That(linkedUserId).IsEqualTo(user.Id);
        var stored = _repo.Get(token.Token);
        await Assert.That(stored!.ConsumedAt).IsNotNull();
        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsEqualTo("987654321");
    }

    [Test]
    public async Task Consume_returns_null_for_unknown_token() {
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var linked = _repo.Consume("doesnotexist", "1", now);
        await Assert.That(linked).IsNull();
    }

    [Test]
    public async Task Consume_returns_null_after_expiry() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var token = _repo.Create(user.Id, now);

        var laterAfterExpiry = now + TelegramLinkTokenRepository.TokenLifetime + TimeSpan.FromMinutes(1);
        var linked = _repo.Consume(token.Token, "1", laterAfterExpiry);

        await Assert.That(linked).IsNull();
        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsNull();
    }

    [Test]
    public async Task Consume_returns_null_when_already_consumed() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var token = _repo.Create(user.Id, now);
        _repo.Consume(token.Token, "111", now);

        var second = _repo.Consume(token.Token, "222", now.AddMinutes(1));

        await Assert.That(second).IsNull();
        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsEqualTo("111");
    }

    [Test]
    public async Task Unlink_clears_chat_id_and_drops_unconsumed_tokens() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var consumed = _repo.Create(user.Id, now);
        _repo.Consume(consumed.Token, "999", now);
        _repo.Create(user.Id, now);

        _repo.Unlink(user.Id);

        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsNull();
        var leftover = Db.TelegramLinkTokens.Where(t => t.UserId == user.Id && t.ConsumedAt == null).ToList();
        await Assert.That(leftover.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PurgeExpired_only_deletes_expired_unconsumed_tokens() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var fresh = _repo.Create(user.Id, now);
        var stale = _repo.Create(user.Id, now - TimeSpan.FromHours(2));
        var consumedStale = _repo.Create(user.Id, now - TimeSpan.FromHours(2));
        _repo.Consume(consumedStale.Token, "5", now - TimeSpan.FromHours(2));

        var purged = _repo.PurgeExpired(now);

        await Assert.That(purged).IsEqualTo(1);
        await Assert.That(_repo.Get(fresh.Token)).IsNotNull();
        await Assert.That(_repo.Get(stale.Token)).IsNull();
        await Assert.That(_repo.Get(consumedStale.Token)).IsNotNull();
    }
}
