using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Server.Events.Subscriptions;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Events;

public class EventReminderRecipientResolverTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
    }

    [Test]
    public async Task Resolves_users_with_email_only() {
        var withEmail = _builder.SeedUser();
        var withoutEmail = _builder.SeedUser();
        Db.Users.Where(u => u.Id == withoutEmail.Id).Set(u => u.Email, "").Update();
        var resolver = new EventReminderRecipientResolver(NotificationTriggers.EventReminder, Db);

        var recipients = resolver.Resolve(new EventReminderNotificationPayload(Guid.NewGuid(), [withEmail.Id, withoutEmail.Id, withEmail.Id]));

        await Assert.That(recipients.Count).IsEqualTo(1);
        await Assert.That(recipients[0].UserId).IsEqualTo(withEmail.Id);
    }

    [Test]
    public async Task Deleted_user_left_out() {
        var deleted = _builder.SeedUser();
        Db.Users.Where(u => u.Id == deleted.Id).Set(u => u.DeletedAt, DateTime.UtcNow).Update();
        var resolver = new EventReminderRecipientResolver(NotificationTriggers.EventDeadlineReminder, Db);

        var recipients = resolver.Resolve(new EventReminderNotificationPayload(Guid.NewGuid(), [deleted.Id]));

        await Assert.That(recipients.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Other_payloads_resolve_nothing() {
        var resolver = new EventReminderRecipientResolver(NotificationTriggers.EventReminder, Db);

        await Assert.That(resolver.Resolve(new object()).Count).IsEqualTo(0);
    }
}
