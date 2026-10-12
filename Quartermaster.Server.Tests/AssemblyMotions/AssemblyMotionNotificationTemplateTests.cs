using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Templates;
using Quartermaster.Data;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionNotificationTemplateTests : RepositoryTestBase {
    private TemplateRepository _templates = default!;

    [Before(Test)]
    public void Setup() {
        _templates = new TemplateRepository(Db);
        _templates.SupplementDefaults();
    }

    private static AssemblyMotionNotificationModel SampleModel(string title = "Beispielantrag") => new() {
        Id = Guid.NewGuid(),
        DisplayNumber = "A001",
        Title = title,
        TitleEscaped = AssemblyMotionNotifier.EscapeMarkdown(title),
        TypeName = "Satzungsaenderung",
        EventName = "Bundesparteitag",
        StatusLabel = "Zugelassen",
        CheckNote = "Alles in Ordnung",
        ChangeNote = "Tippfehler",
        InquiryQuestion = "Warum?",
        InquiryDueAt = "20.10. um 18:00 Uhr",
        Deadline = "25.10. um 23:59 Uhr",
        Path = "/AssemblyMotions/abc",
        OtherDisplayNumber = "A002",
        OtherTitle = "Anderer Antrag",
        OtherTitleEscaped = "Anderer Antrag"
    };

    private static Dictionary<string, object> ModelOf(AssemblyMotionNotificationModel motion) => new() {
        ["motion"] = motion,
        ["globals"] = new TemplateGlobalsDTO { BaseUrl = "https://qm.test", AppName = "Quartermaster", Now = DateTime.UtcNow }
    };

    [Test]
    public async Task Catalog_covers_every_trigger() {
        var catalogIds = NotificationTriggerCatalog.All.Select(d => d.TriggerId).ToHashSet();

        foreach (var triggerId in AssemblyMotionRecipientResolver.TriggerIds)
            await Assert.That(catalogIds.Contains(triggerId)).IsTrue();
    }

    [Test]
    public async Task Every_trigger_has_email_and_telegram_seed() {
        foreach (var triggerId in AssemblyMotionRecipientResolver.TriggerIds) {
            var email = _templates.GetSystemBase($"notifications.{triggerId}.email");
            var telegram = _templates.GetSystemBase($"notifications.{triggerId}.telegram");

            await Assert.That(email).IsNotNull();
            await Assert.That(string.IsNullOrWhiteSpace(email!.Subject)).IsFalse();
            await Assert.That(string.IsNullOrWhiteSpace(email.Body)).IsFalse();
            await Assert.That(telegram).IsNotNull();
            await Assert.That(string.IsNullOrWhiteSpace(telegram!.Body)).IsFalse();
        }
    }

    [Test]
    public async Task Every_seed_renders_against_the_sample_model() {
        foreach (var triggerId in AssemblyMotionRecipientResolver.TriggerIds) {
            foreach (var channel in new[] { "email", "telegram" }) {
                var template = _templates.GetSystemBase($"notifications.{triggerId}.{channel}")!;

                var (body, bodyError) = await TemplateRenderer.RenderTextAsync(template.Body, ModelOf(SampleModel()));
                await Assert.That(bodyError).IsNull();
                await Assert.That(body!).Contains("https://qm.test/AssemblyMotions/abc");

                if (template.Subject != null) {
                    var (subject, subjectError) = await TemplateRenderer.RenderTextAsync(template.Subject, ModelOf(SampleModel()));
                    await Assert.That(subjectError).IsNull();
                    await Assert.That(subject!).Contains("Beispielantrag");
                }
            }
        }
    }

    private const string HostileTitle = "[Klick](https://evil.example) *x* _y_";

    [Test]
    public async Task Hostile_title_is_literal_in_every_email_body() {
        foreach (var triggerId in AssemblyMotionRecipientResolver.TriggerIds) {
            var template = _templates.GetSystemBase($"notifications.{triggerId}.email")!;
            var model = SampleModel(HostileTitle) with { OtherTitle = HostileTitle, OtherTitleEscaped = AssemblyMotionNotifier.EscapeMarkdown(HostileTitle) };

            var (html, error) = await TemplateRenderer.RenderHtmlAsync(template.Body, ModelOf(model));

            await Assert.That(error).IsNull();
            await Assert.That(Regex.IsMatch(html!, "<a [^>]*evil\\.example")).IsFalse();
            await Assert.That(html!).DoesNotContain("<em>x</em>");
            await Assert.That(html!).DoesNotContain("<em>y</em>");
            await Assert.That(html!).DoesNotContain("<strong>x");
            await Assert.That(html!).Contains("[Klick](https://evil.example)");
        }
    }

    [Test]
    public async Task Template_syntax_in_title_stays_literal() {
        var template = _templates.GetSystemBase("notifications.assembly_motion_withdrawn.email")!;

        var (body, error) = await TemplateRenderer.RenderTextAsync(template.Body, ModelOf(SampleModel("Titel {{ globals.BaseUrl }} {% if %}")));

        await Assert.That(error).IsNull();
        await Assert.That(body!).Contains("Titel {{ globals.BaseUrl }} {% if %}");
    }

    [Test]
    public async Task Subjects_use_the_plain_title() {
        var template = _templates.GetSystemBase("notifications.assembly_motion_withdrawn.email")!;

        var (subject, _) = await TemplateRenderer.RenderTextAsync(template.Subject!, ModelOf(SampleModel(HostileTitle)));

        await Assert.That(subject!).Contains(HostileTitle);
    }

    [Test]
    public async Task EscapeMarkdown_escapes_special_characters_and_flattens_newlines() {
        var escaped = AssemblyMotionNotifier.EscapeMarkdown("a\\b `c` *d* _e_ [f](g) <h> #i |j| ~k~\r\nzweite {{ x }}");

        await Assert.That(escaped).IsEqualTo("a\\\\b \\`c\\` \\*d\\* \\_e\\_ \\[f\\]\\(g\\) \\<h\\> \\#i \\|j\\| \\~k\\~  zweite {{ x }}");
    }

    [Test]
    public async Task Every_seed_renders_the_mock_title() {
        var mock = TemplateMockDataProvider.GetMockData("AssemblyMotionNotificationModel");
        var title = ((AssemblyMotionNotificationModel)mock["motion"]).Title;

        foreach (var triggerId in AssemblyMotionRecipientResolver.TriggerIds) {
            foreach (var channel in new[] { "email", "telegram" }) {
                var template = _templates.GetSystemBase($"notifications.{triggerId}.{channel}")!;

                var (body, error) = await TemplateRenderer.RenderTextAsync(template.Body, mock);

                await Assert.That(error).IsNull();
                await Assert.That(body!).Contains(title);
            }
        }
    }

    [Test]
    public async Task Check_note_block_only_when_note_present() {
        var template = _templates.GetSystemBase("notifications.assembly_motion_checked.email")!;

        var (withNote, _) = await TemplateRenderer.RenderTextAsync(template.Body, ModelOf(SampleModel()));
        var (withoutNote, _) = await TemplateRenderer.RenderTextAsync(template.Body, ModelOf(SampleModel() with { CheckNote = "" }));

        await Assert.That(withNote!).Contains("Alles in Ordnung");
        await Assert.That(withoutNote!).DoesNotContain("Hinweis der Antragskommission");
    }

    [Test]
    public async Task Question_text_is_only_in_the_asked_email() {
        var asked = _templates.GetSystemBase("notifications.assembly_motion_inquiry_asked.email")!;
        var askedTelegram = _templates.GetSystemBase("notifications.assembly_motion_inquiry_asked.telegram")!;
        var answered = _templates.GetSystemBase("notifications.assembly_motion_inquiry_answered.email")!;

        var (askedBody, _) = await TemplateRenderer.RenderTextAsync(asked.Body, ModelOf(SampleModel()));
        var (askedTelegramBody, _) = await TemplateRenderer.RenderTextAsync(askedTelegram.Body, ModelOf(SampleModel()));
        var (answeredBody, _) = await TemplateRenderer.RenderTextAsync(answered.Body, ModelOf(SampleModel()));

        await Assert.That(askedBody!).Contains("Warum?");
        await Assert.That(askedTelegramBody!).DoesNotContain("Warum?");
        await Assert.That(answeredBody!).DoesNotContain("Warum?");
    }

    [Test]
    public async Task Model_lookup_maps_triggers_to_the_notification_model() {
        var models = TemplateModelLookup.BuildForTemplate("notifications.assembly_motion_lapsed.email", false, false, false);

        await Assert.That(models).IsEqualTo("AssemblyMotionNotificationModel");
    }
}
