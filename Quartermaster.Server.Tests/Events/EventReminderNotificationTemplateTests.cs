using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.Events;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Events.Subscriptions;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Events;

public class EventReminderNotificationTemplateTests : RepositoryTestBase {
    private TemplateRepository _templates = default!;

    [Before(Test)]
    public void Setup() {
        _templates = new TemplateRepository(Db);
        _templates.SupplementDefaults();
    }

    private static EventReminderNotificationModel SampleModel(string kind) => new() {
        Name = "Bundesparteitag",
        NameEscaped = "Bundesparteitag",
        Date = "28.11.2026",
        Time = "10:00",
        Kind = kind,
        TypeNames = "Satzungsänderungsantrag, Programmantrag",
        MeetingUrl = kind == EventReminderNotificationModel.KindSoon ? "https://meet.test/bpt" : "",
        Path = "/Events/abc"
    };

    private static Dictionary<string, object> ModelOf(EventReminderNotificationModel model) => new() {
        ["event"] = model,
        ["globals"] = new TemplateGlobalsDTO { BaseUrl = "https://qm.test", AppName = "Quartermaster", Now = DateTime.UtcNow }
    };

    private async Task<(string Subject, string Body)> RenderAsync(string triggerId, string channel, EventReminderNotificationModel model) {
        var template = _templates.GetSystemBase($"notifications.{triggerId}.{channel}")!;
        var (body, bodyError) = await TemplateRenderer.RenderTextAsync(template.Body, ModelOf(model));
        await Assert.That(bodyError).IsNull();
        if (template.Subject == null)
            return ("", body!);
        var (subject, subjectError) = await TemplateRenderer.RenderTextAsync(template.Subject, ModelOf(model));
        await Assert.That(subjectError).IsNull();
        return (subject!, body!);
    }

    [Test]
    public async Task Catalog_covers_both_triggers() {
        var catalogIds = NotificationTriggerCatalog.All.Select(d => d.TriggerId).ToHashSet();

        foreach (var triggerId in EventReminderRecipientResolver.TriggerIds)
            await Assert.That(catalogIds.Contains(triggerId)).IsTrue();
    }

    [Test]
    [Arguments(NotificationTriggers.EventDeadlineReminder, "")]
    [Arguments(NotificationTriggers.EventReminder, EventReminderNotificationModel.KindDayBefore)]
    [Arguments(NotificationTriggers.EventReminder, EventReminderNotificationModel.KindToday)]
    [Arguments(NotificationTriggers.EventReminder, EventReminderNotificationModel.KindSoon)]
    [Arguments(NotificationTriggers.EventReminder, EventReminderNotificationModel.KindChanged)]
    public async Task Every_seed_renders_and_links_the_path(string triggerId, string kind) {
        foreach (var channel in new[] { "email", "telegram" }) {
            var (subject, body) = await RenderAsync(triggerId, channel, SampleModel(kind));

            await Assert.That(body).Contains("https://qm.test/Events/abc");
            await Assert.That(body).Contains("Bundesparteitag");
            if (channel == "email")
                await Assert.That(subject).Contains("Bundesparteitag");
        }
    }

    [Test]
    public async Task Deadline_seed_names_types_and_time() {
        var (subject, body) = await RenderAsync(NotificationTriggers.EventDeadlineReminder, "email", SampleModel(""));

        await Assert.That(subject).IsEqualTo("Antragsfrist endet bald: Bundesparteitag");
        await Assert.That(body).Contains("die Antragsfrist für Satzungsänderungsantrag, Programmantrag endet am 28.11.2026 um 10:00 Uhr.");
    }

    [Test]
    [Arguments(EventReminderNotificationModel.KindDayBefore, "Bundesparteitag beginnt morgen")]
    [Arguments(EventReminderNotificationModel.KindToday, "Bundesparteitag beginnt heute")]
    [Arguments(EventReminderNotificationModel.KindSoon, "Bundesparteitag beginnt in 30 Minuten")]
    [Arguments(EventReminderNotificationModel.KindChanged, "Neuer Termin: Bundesparteitag")]
    public async Task Reminder_subject_follows_the_kind(string kind, string expected) {
        var (subject, _) = await RenderAsync(NotificationTriggers.EventReminder, "email", SampleModel(kind));

        await Assert.That(subject).IsEqualTo(expected);
    }

    [Test]
    public async Task Soon_seed_contains_meeting_link() {
        foreach (var channel in new[] { "email", "telegram" }) {
            var (_, soon) = await RenderAsync(NotificationTriggers.EventReminder, channel, SampleModel(EventReminderNotificationModel.KindSoon));
            var (_, dayBefore) = await RenderAsync(NotificationTriggers.EventReminder, channel, SampleModel(EventReminderNotificationModel.KindDayBefore));

            await Assert.That(soon).Contains("https://meet.test/bpt");
            await Assert.That(dayBefore).DoesNotContain("Zum Treffen");
        }
    }

    [Test]
    public async Task Changed_seed_says_noch_offen() {
        var model = SampleModel(EventReminderNotificationModel.KindChanged) with { Date = "noch offen", Time = "" };

        var (_, body) = await RenderAsync(NotificationTriggers.EventReminder, "email", model);

        await Assert.That(body).Contains("hat sich geändert: noch offen.");
        await Assert.That(body).DoesNotContain("Uhr");
    }

    [Test]
    public async Task Changed_seed_without_start_time_has_only_the_date() {
        var model = SampleModel(EventReminderNotificationModel.KindChanged) with { Time = "" };

        var (_, body) = await RenderAsync(NotificationTriggers.EventReminder, "email", model);

        await Assert.That(body).Contains("hat sich geändert: 28.11.2026.");
    }

    [Test]
    public async Task Model_lookup_and_mock_data_cover_the_triggers() {
        var models = TemplateModelLookup.BuildForTemplate($"notifications.{NotificationTriggers.EventReminder}.email", false, false, false);
        var mock = TemplateMockDataProvider.GetMockData("EventReminderNotificationModel");

        await Assert.That(models).IsEqualTo("EventReminderNotificationModel");
        await Assert.That(mock.ContainsKey("event")).IsTrue();
    }
}
