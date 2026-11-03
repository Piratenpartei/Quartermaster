using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Chapters;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Chapters;

public class ChapterContactEmailReadTests : IntegrationTestBase {
    private const string Contact = "vorstand@ov-kontakt.example";

    [Test]
    public async Task Detail_shows_the_contact_only_to_signed_in_users() {
        var chapter = Builder.SeedChapter("OV Kontakt", contactEmail: Contact);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var visitor = AnonymousClient();
        using var user = AuthenticatedClient(token);

        var asVisitor = await visitor.GetStringAsync($"/api/chapters/{chapter.Id}");
        var asUser = await user.GetStringAsync($"/api/chapters/{chapter.Id}");

        await Assert.That(asVisitor).Contains("OV Kontakt");
        await Assert.That(asVisitor).DoesNotContain(Contact);
        await Assert.That(asUser).Contains(Contact);
    }

    [Test]
    public async Task Search_and_list_show_the_contact_only_to_signed_in_users() {
        Builder.SeedChapter("OV Kontakt", contactEmail: Contact);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var visitor = AnonymousClient();
        using var user = AuthenticatedClient(token);
        var searchUrl = "/api/chapters/search?query=Kontakt&page=1&pageSize=25";

        var searchAsVisitor = await visitor.GetStringAsync(searchUrl);
        var listAsVisitor = await visitor.GetStringAsync("/api/chapters");
        var searchAsUser = await user.GetFromJsonAsync<ChapterSearchResponse>(searchUrl);
        var listAsUser = await user.GetFromJsonAsync<ChapterDTO[]>("/api/chapters");

        await Assert.That(searchAsVisitor).Contains("OV Kontakt");
        await Assert.That(searchAsVisitor).DoesNotContain(Contact);
        await Assert.That(listAsVisitor).DoesNotContain(Contact);
        await Assert.That(searchAsUser!.Items.Single().ContactEmail).IsEqualTo(Contact);
        await Assert.That(listAsUser!.Single().ContactEmail).IsEqualTo(Contact);
    }

    [Test]
    public async Task Tree_endpoints_never_show_the_contact() {
        var root = Builder.SeedChapter("LV Kontakt", contactEmail: Contact);
        Builder.SeedChapter("OV Kontakt", parentChapterId: root.Id, contactEmail: Contact);
        using var visitor = AnonymousClient();

        var roots = await visitor.GetStringAsync("/api/chapters/roots");
        var children = await visitor.GetStringAsync($"/api/chapters/{root.Id}/children");

        await Assert.That(roots).Contains("LV Kontakt");
        await Assert.That(roots).DoesNotContain(Contact);
        await Assert.That(children).Contains("OV Kontakt");
        await Assert.That(children).DoesNotContain(Contact);
    }

    [Test]
    public async Task Officers_are_public_without_member_numbers() {
        var chapter = Builder.SeedChapter("OV Vorstand");
        var member = Builder.SeedMember(chapterId: chapter.Id, memberNumber: 987654, firstName: "Vera", lastName: "Vorsitz");
        Builder.SeedChapterOfficer(member.Id, chapter.Id, ChapterOfficerType.Captain);
        using var visitor = AnonymousClient();

        var body = await visitor.GetStringAsync($"/api/chapters/{chapter.Id}");

        await Assert.That(body).Contains("Vorsitz");
        await Assert.That(body).DoesNotContain("987654");
    }
}

public class ChapterContactEmailUpdateTests : IntegrationTestBase {
    [Test]
    public async Task Stores_the_contact_trimmed_and_clears_it_when_empty() {
        var chapter = Builder.SeedChapter("OV");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var set = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "OV", ContactEmail = "  vorstand@ov.example " });
        var stored = Db.Chapters.Single(c => c.Id == chapter.Id).ContactEmail;
        var cleared = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "OV", ContactEmail = " " });

        await Assert.That(set.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(stored).IsEqualTo("vorstand@ov.example");
        await Assert.That(cleared.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Chapters.Single(c => c.Id == chapter.Id).ContactEmail).IsNull();
    }

    [Test]
    [Arguments("vorstand at ov.example", "error.chapter.contact_email_invalid")]
    [Arguments("vor stand@ov.example", "error.chapter.contact_email_invalid")]
    [Arguments("long", "error.chapter.contact_email_too_long")]
    public async Task Refuses_an_unusable_contact(string value, string error) {
        var chapter = Builder.SeedChapter("OV");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var contact = value == "long" ? new string('a', 250) + "@ov.example" : value;

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "OV", ContactEmail = contact });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(error);
        await Assert.That(Db.Chapters.Single(c => c.Id == chapter.Id).ContactEmail).IsNull();
    }
}
