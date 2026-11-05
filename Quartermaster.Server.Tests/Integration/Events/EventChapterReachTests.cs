using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

/// <summary>
/// A higher chapter may always manage the events of lower chapters, since a state chapter sometimes runs events for one of its chapters.
/// The delegate role covers only the delegated chapter.
/// </summary>
public class EventChapterReachTests : IntegrationTestBase {
    private Chapter _state = default!;
    private Chapter _district = default!;
    private string _boardMemberToken = "";
    private string _delegateToken = "";

    [Before(Test)]
    public void SeedChapters() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        _state = chain[1];
        _district = chain[2];
        _boardMemberToken = SeedHolderOf(PermissionIdentifier.SystemRole.ChapterOfficer);
        _delegateToken = SeedHolderOf(PermissionIdentifier.SystemRole.GeneralChapterDelegate);
    }

    private string SeedHolderOf(string roleIdentifier) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var role = new RoleRepository(Db).GetByIdentifier(roleIdentifier)!;
        Builder.AssignRoleToUser(user.Id, role.Id, _state.Id);
        return token;
    }

    /// <summary>Runs the action on a fresh district event for each holder, since most actions change the event.</summary>
    private async Task AssertBoardMemberMayAndDelegateMayNot(Func<HttpClient, Event, EventChecklistItem, Task<HttpResponseMessage>> action) {
        await Assert.That(await Run(_boardMemberToken, action)).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await Run(_delegateToken, action)).IsEqualTo(HttpStatusCode.Forbidden);
    }

    private async Task<HttpStatusCode> Run(string token, Func<HttpClient, Event, EventChecklistItem, Task<HttpResponseMessage>> action) {
        var ev = Builder.SeedEvent(_district.Id, status: EventStatus.Active);
        var item = Builder.SeedChecklistItem(ev.Id);
        Builder.SeedChecklistItem(ev.Id, sortOrder: 1);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await action(client, ev, item);
        return response.StatusCode;
    }

    [Test]
    public async Task Create() {
        await AssertBoardMemberMayAndDelegateMayNot((client, _, _) => client.PostAsJsonAsync("/api/events",
            new EventCreateRequest { ChapterId = _district.Id, InternalName = "Kreisparteitag", PublicName = "Kreisparteitag" }));
    }

    [Test]
    public async Task Create_from_template() {
        var template = Builder.SeedEventTemplate(chapterId: _district.Id);

        await AssertBoardMemberMayAndDelegateMayNot((client, _, _) => client.PostAsJsonAsync("/api/events/from-template",
            new EventFromTemplateRequest { TemplateId = template.Id, ChapterId = _district.Id, VariableValues = new Dictionary<string, string>() }));
    }

    [Test]
    public async Task Update() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, _) => client.PutAsJsonAsync($"/api/events/{ev.Id}",
            new EventUpdateRequest { Id = ev.Id, InternalName = "Geändert", PublicName = "Geändert" }));
    }

    [Test]
    public async Task Change_status() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, _) => client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new { Status = EventStatus.Completed }));
    }

    [Test]
    public async Task Archive() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, _) => client.PostAsJsonAsync($"/api/events/{ev.Id}/archive", new { }));
    }

    [Test]
    public async Task Add_checklist_item() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, _) => client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist",
            new ChecklistItemCreateRequest { EventId = ev.Id, Label = "Raum buchen", ItemType = ChecklistItemType.Text }));
    }

    [Test]
    public async Task Check_and_uncheck_checklist_item() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, item) => client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/check",
            new ChecklistItemCheckRequest { EventId = ev.Id, ItemId = item.Id, ExecuteAction = true }));
        await AssertBoardMemberMayAndDelegateMayNot(async (client, ev, item) => {
            await client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/check",
                new ChecklistItemCheckRequest { EventId = ev.Id, ItemId = item.Id, ExecuteAction = true });
            return await client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/uncheck", new { });
        });
    }

    [Test]
    public async Task Update_checklist_item() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, item) => client.PutAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}",
            new ChecklistItemUpdateRequest { EventId = ev.Id, ItemId = item.Id, Label = "Geändert", ItemType = ChecklistItemType.Text }));
    }

    [Test]
    public async Task Reorder_checklist_item() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, item) => client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/reorder",
            new { EventId = ev.Id, ItemId = item.Id, Direction = 1 }));
    }

    [Test]
    public async Task Delete_checklist_item() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, item) => client.DeleteAsync($"/api/events/{ev.Id}/checklist/{item.Id}"));
    }

    [Test]
    public async Task Preview_checklist_recipients() {
        await AssertBoardMemberMayAndDelegateMayNot((client, ev, _) => client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/resolve-recipients",
            new RecipientPreviewRequest { EventId = ev.Id, Configuration = new EventChecklistItemConfigDTO { TargetType = "Chapter", TargetId = _district.Id } }));
    }

    [Test]
    public async Task Detail_offers_edit_and_archive() {
        var ev = Builder.SeedEvent(_district.Id, visibility: EventVisibility.MembersOnly);

        var boardMemberView = await DetailAs(_boardMemberToken, ev.Id);
        var delegateView = await DetailAs(_delegateToken, ev.Id);

        await Assert.That(boardMemberView.CanEdit).IsTrue();
        await Assert.That(boardMemberView.CanArchive).IsTrue();
        await Assert.That(delegateView.CanEdit).IsFalse();
        await Assert.That(delegateView.CanArchive).IsFalse();
    }

    private async Task<EventDetailDTO> DetailAs(string token, Guid eventId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return (await client.GetFromJsonAsync<EventDetailDTO>($"/api/events/{eventId}"))!;
    }
}
