using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Submissions;

public class SubmissionConfirmEndpointTests : IntegrationTestBase {
    private async Task<HttpClient> AnonymousClientWithCsrfAsync() {
        var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private async Task<string> SubmitMotionAsync(Guid chapterId) {
        using var client = await AnonymousClientWithCsrfAsync();
        await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = "Author",
            AuthorEmail = "author@test.local",
            Title = "Confirm Me",
            Text = "Body"
        });
        return Db.PendingSubmissions.Single(p => p.ConfirmedAt == null).Token;
    }

    [Test]
    public async Task Submit_sends_confirmation_email_to_submitter() {
        var chapter = Builder.SeedChapter("C");
        await SubmitMotionAsync(chapter.Id);

        var emailLogs = Db.NotificationLogs.Where(l => l.ChannelId == "email").ToList();
        await Assert.That(emailLogs.Count).IsEqualTo(1);
        await Assert.That(emailLogs[0].Recipient).IsEqualTo("author@test.local");
        await Assert.That(emailLogs[0].Body!.Contains("/Confirm/")).IsTrue();
    }

    [Test]
    public async Task Confirm_materializes_entity_and_reports_confirmed() {
        var chapter = Builder.SeedChapter("C");
        var token = await SubmitMotionAsync(chapter.Id);

        using var client = await AnonymousClientWithCsrfAsync();
        var resp = await client.PostAsync($"/api/submissions/{token}/confirm", null);
        var dto = await resp.Content.ReadFromJsonAsync<SubmissionConfirmResultDTO>();

        await Assert.That(dto!.Status).IsEqualTo(SubmissionConfirmStatus.Confirmed);
        await Assert.That(Db.Motions.Count(m => m.Title == "Confirm Me")).IsEqualTo(1);
    }

    [Test]
    public async Task Confirm_unknown_token_reports_not_found() {
        using var client = await AnonymousClientWithCsrfAsync();
        var resp = await client.PostAsync("/api/submissions/doesnotexist/confirm", null);
        var dto = await resp.Content.ReadFromJsonAsync<SubmissionConfirmResultDTO>();
        await Assert.That(dto!.Status).IsEqualTo(SubmissionConfirmStatus.NotFound);
    }

    [Test]
    public async Task Confirm_twice_is_idempotent_no_duplicate_entity() {
        var chapter = Builder.SeedChapter("C");
        var token = await SubmitMotionAsync(chapter.Id);

        using var client = await AnonymousClientWithCsrfAsync();
        var first = await client.PostAsync($"/api/submissions/{token}/confirm", null);
        var firstDto = await first.Content.ReadFromJsonAsync<SubmissionConfirmResultDTO>();
        var second = await client.PostAsync($"/api/submissions/{token}/confirm", null);
        var secondDto = await second.Content.ReadFromJsonAsync<SubmissionConfirmResultDTO>();

        await Assert.That(firstDto!.Status).IsEqualTo(SubmissionConfirmStatus.Confirmed);
        await Assert.That(secondDto!.Status).IsEqualTo(SubmissionConfirmStatus.AlreadyConfirmed);
        await Assert.That(Db.Motions.Count(m => m.Title == "Confirm Me")).IsEqualTo(1);
    }

    [Test]
    public async Task Confirm_expired_token_reports_expired_and_creates_nothing() {
        var chapter = Builder.SeedChapter("C");
        var payload = JsonSerializer.Serialize(new MotionCreateRequest {
            ChapterId = chapter.Id, AuthorName = "A", AuthorEmail = "a@t.local", Title = "Expired", Text = "x"
        });
        Db.Insert(new PendingSubmission {
            Token = "expiredtoken",
            Kind = PendingSubmissionKind.Motion,
            PayloadJson = payload,
            Email = "a@t.local",
            CreatedAt = DateTime.UtcNow.AddHours(-50),
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            ConfirmedAt = null
        });

        using var client = await AnonymousClientWithCsrfAsync();
        var resp = await client.PostAsync("/api/submissions/expiredtoken/confirm", null);
        var dto = await resp.Content.ReadFromJsonAsync<SubmissionConfirmResultDTO>();

        await Assert.That(dto!.Status).IsEqualTo(SubmissionConfirmStatus.Expired);
        await Assert.That(Db.Motions.Count()).IsEqualTo(0);
    }
}
