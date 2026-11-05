using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

/// <summary>
/// Event templates are seen both ways, so a higher chapter publishes a template and the lower chapters use it; editing works top down
/// like the event rights. Templates without a chapter keep needing the global right.
/// </summary>
public class EventTemplateReachTests : IntegrationTestBase {
    private Chapter _federal = default!;
    private Chapter _state = default!;
    private Chapter _district = default!;
    private Chapter _otherState = default!;

    [Before(Test)]
    public void SeedChapters() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        _federal = chain[0];
        _state = chain[1];
        _district = chain[2];
        _otherState = Builder.SeedChapter("Anderes Land", parentChapterId: _federal.Id);
    }

    private async Task<HttpClient> BoardMemberOf(Guid chapterId) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var role = new RoleRepository(Db).GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        Builder.AssignRoleToUser(user.Id, role.Id, chapterId);
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private static async Task<Guid[]> ListedTemplates(HttpClient client) {
        var templates = await client.GetFromJsonAsync<List<EventTemplateDTO>>("/api/eventtemplates");
        return templates!.Select(t => t.Id).ToArray();
    }

    [Test]
    public async Task A_lower_chapter_sees_and_uses_the_templates_of_the_chapters_above() {
        var stateTemplate = Builder.SeedEventTemplate(chapterId: _state.Id);
        var federalTemplate = Builder.SeedEventTemplate(chapterId: _federal.Id);
        using var district = await BoardMemberOf(_district.Id);

        var detail = await district.GetAsync($"/api/eventtemplates/{stateTemplate.Id}");
        var created = await district.PostAsJsonAsync("/api/events/from-template",
            new EventFromTemplateRequest { TemplateId = stateTemplate.Id, ChapterId = _district.Id, VariableValues = new Dictionary<string, string>() });

        await Assert.That(await ListedTemplates(district)).IsEquivalentTo(new[] { stateTemplate.Id, federalTemplate.Id });
        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_higher_chapter_sees_and_deletes_the_templates_of_the_chapters_below() {
        var districtTemplate = Builder.SeedEventTemplate(chapterId: _district.Id);
        using var state = await BoardMemberOf(_state.Id);

        var listed = await ListedTemplates(state);
        var deleted = await state.DeleteAsync($"/api/eventtemplates/{districtTemplate.Id}");

        await Assert.That(listed).Contains(districtTemplate.Id);
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_lower_chapter_cannot_delete_a_template_of_a_chapter_above() {
        var stateTemplate = Builder.SeedEventTemplate(chapterId: _state.Id);
        using var district = await BoardMemberOf(_district.Id);

        var deleted = await district.DeleteAsync($"/api/eventtemplates/{stateTemplate.Id}");

        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Templates_of_another_branch_stay_hidden_and_unusable() {
        var foreignTemplate = Builder.SeedEventTemplate(chapterId: _otherState.Id);
        using var district = await BoardMemberOf(_district.Id);

        var detail = await district.GetAsync($"/api/eventtemplates/{foreignTemplate.Id}");
        var created = await district.PostAsJsonAsync("/api/events/from-template",
            new EventFromTemplateRequest { TemplateId = foreignTemplate.Id, ChapterId = _district.Id, VariableValues = new Dictionary<string, string>() });

        await Assert.That(await ListedTemplates(district)).DoesNotContain(foreignTemplate.Id);
        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Templates_without_a_chapter_need_the_global_right() {
        var globalTemplate = Builder.SeedEventTemplate();
        using var district = await BoardMemberOf(_district.Id);

        var detail = await district.GetAsync($"/api/eventtemplates/{globalTemplate.Id}");

        await Assert.That(await ListedTemplates(district)).DoesNotContain(globalTemplate.Id);
        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task A_higher_chapter_saves_a_lower_chapters_event_as_a_template() {
        var ev = Builder.SeedEvent(_district.Id, status: EventStatus.Draft, visibility: EventVisibility.MembersOnly);
        using var state = await BoardMemberOf(_state.Id);

        var detail = await state.GetFromJsonAsync<EventDetailDTO>($"/api/events/{ev.Id}");
        var saved = await state.PostAsJsonAsync("/api/eventtemplates", new EventTemplateCreateRequest { EventId = ev.Id, Name = "Kreisparteitag" });

        await Assert.That(detail!.CanSaveAsTemplate).IsTrue();
        await Assert.That(saved.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
