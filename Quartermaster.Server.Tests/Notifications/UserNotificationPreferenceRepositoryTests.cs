using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Notifications;

public class UserNotificationPreferenceRepositoryTests : RepositoryTestBase {
    private UserNotificationPreferenceRepository _repo = default!;
    private TestDataBuilder _builder = default!;

    [Before(Test)]
    public void Setup() {
        _repo = new UserNotificationPreferenceRepository(Db);
        _builder = new TestDataBuilder(Db);
    }

    [Test]
    public async Task IsEnabled_returns_default_when_no_row() {
        var user = _builder.SeedUser();
        var result = _repo.IsEnabled(user.Id, "motion_submitted", "smtp", defaultIfMissing: true);
        await Assert.That(result).IsTrue();
        result = _repo.IsEnabled(user.Id, "motion_submitted", "smtp", defaultIfMissing: false);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsEnabled_returns_explicit_override() {
        var user = _builder.SeedUser();
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "smtp", Enabled = false
        });
        var result = _repo.IsEnabled(user.Id, "motion_submitted", "smtp", defaultIfMissing: true);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Replace_wipes_old_and_inserts_new() {
        var user = _builder.SeedUser();
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "old_trigger", ChannelId = "smtp", Enabled = true
        });

        _repo.Replace(user.Id, new[] {
            new UserNotificationPreference { TriggerId = "motion_submitted", ChannelId = "smtp", Enabled = false },
            new UserNotificationPreference { TriggerId = "application_submitted", ChannelId = "smtp", Enabled = true }
        });

        var rows = _repo.GetForUser(user.Id);
        await Assert.That(rows.Count).IsEqualTo(2);
        await Assert.That(rows.Any(r => r.TriggerId == "old_trigger")).IsFalse();
        await Assert.That(rows.Single(r => r.TriggerId == "motion_submitted").Enabled).IsFalse();
        await Assert.That(rows.Single(r => r.TriggerId == "application_submitted").Enabled).IsTrue();
    }

    [Test]
    public async Task Replace_with_empty_list_clears_user() {
        var user = _builder.SeedUser();
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "smtp", Enabled = true
        });
        _repo.Replace(user.Id, Array.Empty<UserNotificationPreference>());
        var rows = _repo.GetForUser(user.Id);
        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Replace_only_touches_target_user() {
        var alice = _builder.SeedUser(firstName: "Alice");
        var bob = _builder.SeedUser(firstName: "Bob");
        Db.Insert(new UserNotificationPreference {
            UserId = alice.Id, TriggerId = "motion_submitted", ChannelId = "smtp", Enabled = false
        });
        Db.Insert(new UserNotificationPreference {
            UserId = bob.Id, TriggerId = "motion_submitted", ChannelId = "smtp", Enabled = false
        });
        _repo.Replace(alice.Id, Array.Empty<UserNotificationPreference>());
        await Assert.That(_repo.GetForUser(alice.Id).Count).IsEqualTo(0);
        await Assert.That(_repo.GetForUser(bob.Id).Count).IsEqualTo(1);
    }
}
