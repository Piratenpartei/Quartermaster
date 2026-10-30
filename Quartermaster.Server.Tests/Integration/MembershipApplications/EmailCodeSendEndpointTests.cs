using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Options;
using Quartermaster.Data.Templates;
using Quartermaster.Server.MembershipApplications.EmailCodes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.MembershipApplications;

public class EmailCodeSendEndpointTests : IntegrationTestBase {
    private const string Route = "/api/membershipapplications/email-code";
    private const string TriggerId = "membership_application_email_code";

    private async Task<HttpResponseMessage> Send(HttpClient client, string email = "alice@test.local", Guid? chapterId = null) {
        return await client.PostAsJsonAsync(Route, new EmailCodeSendRequest { Email = email, ChapterId = chapterId });
    }

    private async Task<HttpResponseMessage> SendAnonymous(string email = "alice@test.local", Guid? chapterId = null) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return await Send(client, email, chapterId);
    }

    private void SetUp() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
    }

    [Test]
    public async Task Sends_code_mail() {
        SetUp();

        var response = await SendAnonymous();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var logs = Db.NotificationLogs.Where(l => l.TriggerId == TriggerId).ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("alice@test.local");
        await Assert.That(logs[0].Subject).IsEqualTo("Bestätigungscode für deinen Mitgliedsantrag - Piratenpartei");
        await Assert.That(logs[0].Body).Contains("15 Minuten");
        var code = Regex.Match(logs[0].Body, @"\d{6}").Value;
        using var scope = Factory.Services.CreateScope();
        var result = scope.ServiceProvider.GetRequiredService<EmailCodeService>().Check("alice@test.local", code, DateTime.UtcNow);
        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Valid);
    }

    [Test]
    public async Task Refused_without_setup() {
        var response = await SendAnonymous();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Admin.Application.NotSetUp);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == TriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Refused_when_form_disabled() {
        SetUp();
        Db.SystemOptions.Where(o => o.Identifier == PublicFormOptions.MembershipApplication && o.ChapterId == null).Set(o => o.Value, "false").Update();

        var response = await SendAnonymous();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.PublicForm.Disabled);
    }

    [Test]
    public async Task Refused_for_implausible_address() {
        SetUp();

        var response = await SendAnonymous("not-an-address");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.AddressInvalid);
        await Assert.That(Db.EmailVerificationCodes.Count()).IsEqualTo(0);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == TriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Refused_for_too_long_address() {
        SetUp();

        var response = await SendAnonymous(new string('a', 320) + "@test.local");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.AddressInvalid);
        await Assert.That(Db.EmailVerificationCodes.Count()).IsEqualTo(0);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == TriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Per_address_limit() {
        SetUp();
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        for (var i = 0; i < 10; i++) {
            var ok = await Send(client);
            await Assert.That(ok.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        var rejected = await Send(client);

        await Assert.That((int)rejected.StatusCode).IsEqualTo(429);
        await Assert.That(await rejected.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.TooManySends);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == TriggerId)).IsEqualTo(10);
    }

    [Test]
    public async Task Per_ip_limit() {
        SetUp();
        Db.SystemOptions
            .Where(o => o.Identifier == "auth.ratelimit.email_code_permits" && o.ChapterId == null)
            .Set(o => o.Value, "2")
            .Update();

        using var strict = Factory.WithWebHostBuilder(_ => { });
        using var client = strict.CreateClient();
        await AttachAntiforgeryTokenAsync(client);

        var first = await Send(client, "a@test.local");
        var second = await Send(client, "b@test.local");
        var third = await Send(client, "c@test.local");

        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((int)third.StatusCode).IsEqualTo(429);
    }

    [Test]
    public async Task Response_reports_lifetime() {
        SetUp();

        var response = await SendAnonymous();

        var body = await response.Content.ReadFromJsonAsync<EmailCodeSendResponse>();
        await Assert.That(body!.CodeRequired).IsTrue();
        await Assert.That(body.LifetimeMinutes).IsEqualTo(15);
    }

    [Test]
    public async Task Uses_chapter_template() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var scope = Factory.Services.CreateScope();
        var templates = scope.ServiceProvider.GetRequiredService<TemplateRepository>();
        var baseRow = templates.GetSystemBase(EmailCodeMailService.TemplateIdentifier)!;
        var overrideRow = new Template {
            Identifier = baseRow.Identifier,
            DisplayName = baseRow.DisplayName,
            ChapterId = chapter.Id,
            BaseTemplateId = baseRow.Id,
            Subject = "Eigener Betreff",
            Body = "Eigener Text {{ code }}",
            AllowsChapterFields = true
        };
        templates.Create(overrideRow);
        try {
            var response = await SendAnonymous(chapterId: chapter.Id);

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            var log = Db.NotificationLogs.Single(l => l.TriggerId == TriggerId);
            await Assert.That(log.Subject).IsEqualTo("Eigener Betreff");
            await Assert.That(log.Body).Contains("Eigener Text");
        } finally {
            Db.Templates.Where(t => t.Id == overrideRow.Id).Delete();
        }
    }

    [Test]
    public async Task Refused_for_unknown_chapter() {
        SetUp();

        var response = await SendAnonymous(chapterId: Guid.NewGuid());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.ChapterNotFound);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == TriggerId)).IsEqualTo(0);
    }
}
