using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ApplicantRegister;

public class ApplicantRegisterEndpointTests : IntegrationTestBase {
    private const string BaseUrl = "/api/admin/applicant-register";

    private Guid _managerId;
    private string _managerToken = "";
    private string _plainToken = "";

    public ApplicantRegisterEndpointTests() {
        (var manager, _managerToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        _managerId = manager.Id;
        (_, _plainToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ViewApplications });
    }

    private static ApplicantRegisterEntrySaveRequest ValidRequest() => new() {
        Kind = ApplicantRegisterKind.Expelled,
        FirstName = "Erika",
        LastName = "Beispiel",
        DateOfBirth = new DateOnly(1980, 5, 6),
        Street = "Hauptstrasse",
        HouseNumber = "7",
        PostCode = "30159",
        City = "Hannover",
        Email = "erika@test.local",
        OccurredOn = new DateOnly(2024, 2, 3),
        Note = "Parteiausschluss"
    };

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string url, object? body = null) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var request = new HttpRequestMessage(method, url);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private async Task<List<string>> ErrorMessagesAsync(HttpResponseMessage response) {
        var problem = await response.Content.ReadFromJsonAsync<FastEndpoints.ProblemDetails>();
        return problem!.Errors.Select(e => e.Reason).ToList();
    }

    [Test]
    public async Task Entry_round_trip_writes_audit_entries() {
        var chapter = Builder.SeedChapter("Bund");
        var request = ValidRequest();
        request.ChapterId = chapter.Id;

        var created = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = (await created.Content.ReadFromJsonAsync<ApplicantRegisterEntryDTO>())!;
        await Assert.That(dto.ChapterName).IsEqualTo("Bund");
        await Assert.That(dto.DateOfBirth).IsEqualTo(new DateOnly(1980, 5, 6));
        await Assert.That(dto.SourceApplicationId).IsNull();
        var stored = Db.ApplicantRegisterEntries.Single(e => e.Id == dto.Id);
        await Assert.That(stored.CreatedByUserId).IsEqualTo(_managerId);
        await Assert.That(stored.SourceApplicationId).IsNull();
        await Assert.That(Db.AuditLogs.Count(a => a.EntityId == dto.Id && a.Action == "Created")).IsEqualTo(1);

        var fetched = await SendAsync(_managerToken, HttpMethod.Get, $"{BaseUrl}/{dto.Id}");
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetchedDto = (await fetched.Content.ReadFromJsonAsync<ApplicantRegisterEntryDTO>())!;
        await Assert.That(fetchedDto.LastName).IsEqualTo("Beispiel");

        request.City = "Celle";
        var updated = await SendAsync(_managerToken, HttpMethod.Put, $"{BaseUrl}/{dto.Id}", request);
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var after = Db.ApplicantRegisterEntries.Single(e => e.Id == dto.Id);
        await Assert.That(after.City).IsEqualTo("Celle");
        await Assert.That(after.CreatedByUserId).IsEqualTo(_managerId);
        await Assert.That(after.CreatedAt).IsEqualTo(stored.CreatedAt);
        var change = Db.AuditLogs.Single(a => a.EntityId == dto.Id && a.Action == "Updated");
        await Assert.That(change.FieldName).IsEqualTo("City");
        await Assert.That(change.OldValue).IsEqualTo("Hannover");
        await Assert.That(change.NewValue).IsEqualTo("Celle");

        var deleted = await SendAsync(_managerToken, HttpMethod.Delete, $"{BaseUrl}/{dto.Id}");
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.Id == dto.Id)).IsEqualTo(0);
        await Assert.That(Db.AuditLogs.Count(a => a.EntityId == dto.Id && a.Action == "Deleted")).IsEqualTo(1);
    }

    [Test]
    public async Task Update_keeps_origin_of_automatic_entry() {
        var application = Builder.SeedMembershipApplication();
        var entry = Builder.SeedRegisterEntry(sourceApplicationId: application.Id);
        var createdAt = Db.ApplicantRegisterEntries.Single(e => e.Id == entry.Id).CreatedAt;
        var request = ValidRequest();
        request.ChapterId = entry.ChapterId;

        var response = await SendAsync(_managerToken, HttpMethod.Put, $"{BaseUrl}/{entry.Id}", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var after = Db.ApplicantRegisterEntries.Single(e => e.Id == entry.Id);
        await Assert.That(after.SourceApplicationId).IsEqualTo(application.Id);
        await Assert.That(after.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Names_alone_are_refused() {
        var request = new ApplicantRegisterEntrySaveRequest {
            FirstName = "Erika",
            LastName = "Beispiel",
            OccurredOn = new DateOnly(2024, 2, 3)
        };

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorMessagesAsync(response)).Contains(I18nKey.Error.ApplicantRegister.IdentifyingDetailRequired);
        await Assert.That(Db.ApplicantRegisterEntries.Count()).IsEqualTo(0);
    }

    [Test]
    [Arguments("", "", "30159", "Hannover", true)]
    [Arguments("Hauptstrasse", "7", "", "", true)]
    [Arguments("Hauptstrasse", "", "", "", false)]
    [Arguments("", "", "30159", "", false)]
    public async Task Address_parts_count_as_identifying_detail(string street, string houseNumber, string postCode, string city, bool accepted) {
        var request = new ApplicantRegisterEntrySaveRequest {
            FirstName = "Erika",
            LastName = "Beispiel",
            Street = street,
            HouseNumber = houseNumber,
            PostCode = postCode,
            City = city,
            OccurredOn = new DateOnly(2024, 2, 3)
        };

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Entry_without_chapter_stores_null() {
        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, ValidRequest());

        var dto = (await response.Content.ReadFromJsonAsync<ApplicantRegisterEntryDTO>())!;
        await Assert.That(dto.ChapterId).IsNull();
        await Assert.That(dto.ChapterName).IsEqualTo("");
        await Assert.That(Db.ApplicantRegisterEntries.Single(e => e.Id == dto.Id).ChapterId).IsNull();
    }

    [Test]
    public async Task Invalid_fields_are_refused() {
        var request = ValidRequest();
        request.FirstName = " ";
        request.Email = "not an address";
        request.OccurredOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        request.Kind = (ApplicantRegisterKind)99;

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var messages = await ErrorMessagesAsync(response);
        await Assert.That(messages).Contains(I18nKey.Error.ApplicantRegister.NamesRequired);
        await Assert.That(messages).Contains(I18nKey.Error.ApplicantRegister.EmailInvalid);
        await Assert.That(messages).Contains(I18nKey.Error.ApplicantRegister.OccurredOnInFuture);
        await Assert.That(messages).Contains(I18nKey.Error.ApplicantRegister.KindInvalid);
    }

    [Test]
    public async Task Too_long_field_is_refused() {
        var request = ValidRequest();
        request.PostCode = new string('1', 17);

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorMessagesAsync(response)).Contains(I18nKey.Error.ApplicantRegister.FieldTooLong);
    }

    [Test]
    public async Task Plain_member_gets_403() {
        var entry = Builder.SeedRegisterEntry();
        var body = ValidRequest();

        var statuses = new[] {
            (await SendAsync(_plainToken, HttpMethod.Get, BaseUrl)).StatusCode,
            (await SendAsync(_plainToken, HttpMethod.Get, $"{BaseUrl}/{entry.Id}")).StatusCode,
            (await SendAsync(_plainToken, HttpMethod.Get, $"{BaseUrl}/{Guid.NewGuid()}")).StatusCode,
            (await SendAsync(_plainToken, HttpMethod.Post, BaseUrl, body)).StatusCode,
            (await SendAsync(_plainToken, HttpMethod.Put, $"{BaseUrl}/{entry.Id}", body)).StatusCode,
            (await SendAsync(_plainToken, HttpMethod.Delete, $"{BaseUrl}/{entry.Id}")).StatusCode,
            (await SendAsync(_plainToken, HttpMethod.Delete, $"{BaseUrl}/{Guid.NewGuid()}")).StatusCode
        };

        await Assert.That(statuses.All(s => s == HttpStatusCode.Forbidden)).IsTrue();
        await Assert.That(Db.ApplicantRegisterEntries.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Unknown_id_is_not_found() {
        var id = Guid.NewGuid();

        await Assert.That((await SendAsync(_managerToken, HttpMethod.Get, $"{BaseUrl}/{id}")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await SendAsync(_managerToken, HttpMethod.Put, $"{BaseUrl}/{id}", ValidRequest())).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await SendAsync(_managerToken, HttpMethod.Delete, $"{BaseUrl}/{id}")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private void SeedHit(Guid entryId, ApplicationStatus status, bool dismissed) {
        var application = Builder.SeedMembershipApplication(status: status);
        Db.Insert(new ApplicationRegisterHit {
            ApplicationId = application.Id,
            EntryId = entryId,
            MatchedFields = RegisterMatchFields.Email,
            CreatedAt = DateTime.UtcNow,
            DismissedAt = dismissed ? DateTime.UtcNow : null
        });
    }

    [Test]
    [Arguments(ApplicationStatus.Pending)]
    [Arguments(ApplicationStatus.PendingDivisionLinking)]
    [Arguments(ApplicationStatus.PendingFederalApproval)]
    public async Task Entry_with_open_hit_of_undecided_application_is_kept(ApplicationStatus status) {
        var entry = Builder.SeedRegisterEntry();
        SeedHit(entry.Id, status, dismissed: false);

        var response = await SendAsync(_managerToken, HttpMethod.Delete, $"{BaseUrl}/{entry.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await ErrorMessagesAsync(response)).Contains(I18nKey.Error.ApplicantRegister.EntryInUse);
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.Id == entry.Id)).IsEqualTo(1);
        await Assert.That(Db.ApplicationRegisterHits.Count(h => h.EntryId == entry.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Entry_with_dismissed_or_decided_hits_is_deleted() {
        var entry = Builder.SeedRegisterEntry();
        SeedHit(entry.Id, ApplicationStatus.PendingFederalApproval, dismissed: true);
        SeedHit(entry.Id, ApplicationStatus.Approved, dismissed: false);
        SeedHit(entry.Id, ApplicationStatus.Rejected, dismissed: false);

        var response = await SendAsync(_managerToken, HttpMethod.Delete, $"{BaseUrl}/{entry.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.Id == entry.Id)).IsEqualTo(0);
        await Assert.That(Db.ApplicationRegisterHits.Count(h => h.EntryId == entry.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Search_by_name() {
        Builder.SeedRegisterEntry(firstName: "Erika", lastName: "Beispiel");
        Builder.SeedRegisterEntry(firstName: "Hans", lastName: "Meier");

        var response = await SendAsync(_managerToken, HttpMethod.Get, $"{BaseUrl}?query=meier&page=1&pageSize=10");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApplicantRegisterEntrySearchResponse>())!;
        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items.Single().LastName).IsEqualTo("Meier");
    }

    [Test]
    public async Task Unknown_chapter_is_refused() {
        var request = ValidRequest();
        request.ChapterId = Guid.NewGuid();

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorMessagesAsync(response)).Contains(I18nKey.Error.ApplicantRegister.ChapterUnknown);
    }

    [Test]
    public async Task Entry_of_a_deleted_chapter_stays_editable() {
        var chapter = Builder.SeedChapter("Aufgeloest");
        var entry = Builder.SeedRegisterEntry(chapterId: chapter.Id);
        Db.Chapters.Where(c => c.Id == chapter.Id).Set(c => c.DeletedAt, DateTime.UtcNow).Update();
        var request = ValidRequest();
        request.ChapterId = chapter.Id;
        request.Note = "Nur die Notiz";

        var response = await SendAsync(_managerToken, HttpMethod.Put, $"{BaseUrl}/{entry.Id}", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var after = Db.ApplicantRegisterEntries.Single(e => e.Id == entry.Id);
        await Assert.That(after.ChapterId).IsEqualTo(chapter.Id);
        await Assert.That(after.Note).IsEqualTo("Nur die Notiz");
    }

    [Test]
    public async Task Padded_values_are_checked_after_trimming_and_stored_trimmed() {
        var request = ValidRequest();
        request.PostCode = "  30159  ";
        request.City = "  " + new string('a', 256) + "  ";
        request.FirstName = "  Erika  ";

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApplicantRegisterEntryDTO>())!;
        var stored = Db.ApplicantRegisterEntries.Single(e => e.Id == dto.Id);
        await Assert.That(stored.PostCode).IsEqualTo("30159");
        await Assert.That(stored.City).IsEqualTo(new string('a', 256));
        await Assert.That(stored.FirstName).IsEqualTo("Erika");
    }

    [Test]
    public async Task Value_too_long_after_trimming_is_refused() {
        var request = ValidRequest();
        request.City = new string('a', 257);

        var response = await SendAsync(_managerToken, HttpMethod.Post, BaseUrl, request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorMessagesAsync(response)).Contains(I18nKey.Error.ApplicantRegister.FieldTooLong);
    }

    [Test]
    public async Task Page_size_above_the_cap_is_refused() {
        Builder.SeedRegisterEntry();

        var response = await SendAsync(_managerToken, HttpMethod.Get, $"{BaseUrl}?page=1&pageSize=100000");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorMessagesAsync(response)).Contains(I18nKey.Error.Validation.PageSizeRange);
    }

    [Test]
    public async Task List_returns_chapter_names_for_every_entry() {
        var first = Builder.SeedChapter("Alpha");
        var second = Builder.SeedChapter("Beta");
        Builder.SeedRegisterEntry(chapterId: first.Id);
        Builder.SeedRegisterEntry(chapterId: second.Id);
        Builder.SeedRegisterEntry(chapterId: first.Id);

        var response = await SendAsync(_managerToken, HttpMethod.Get, $"{BaseUrl}?page=1&pageSize=10");

        var result = (await response.Content.ReadFromJsonAsync<ApplicantRegisterEntrySearchResponse>())!;
        await Assert.That(result.Items.Count(i => i.ChapterName == "Alpha")).IsEqualTo(2);
        await Assert.That(result.Items.Count(i => i.ChapterName == "Beta")).IsEqualTo(1);
    }
}
