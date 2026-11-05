using System;
using System.Linq;
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
/// The management list and the internal data of an event (internal name, checklist) are for those who may view or edit the events of
/// its chapter, directly or from a chapter above; the public list and detail show what a visitor may see.
/// </summary>
public class EventManagementAccessTests : IntegrationTestBase {
    private Chapter _state = default!;
    private Chapter _district = default!;
    private Chapter _otherState = default!;

    [Before(Test)]
    public void SeedChapters() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        _state = chain[1];
        _district = chain[2];
        _otherState = Builder.SeedChapter("Anderes Land", parentChapterId: chain[0].Id);
    }

    private string SeedHolderOf(string roleIdentifier, Guid chapterId) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var role = new RoleRepository(Db).GetByIdentifier(roleIdentifier)!;
        Builder.AssignRoleToUser(user.Id, role.Id, chapterId);
        return token;
    }

    private async Task<HttpClient> ClientFor(string? token) {
        return token == null ? AnonymousClient() : await AuthenticatedClientWithCsrfAsync(token);
    }

    private async Task<Guid[]> ListAs(string? token, bool managed) {
        using var client = await ClientFor(token);
        var response = await client.GetFromJsonAsync<EventSearchResponse>($"/api/events?page=1&pageSize=50&managed={managed}");
        return response!.Items.Select(e => e.Id).ToArray();
    }

    private async Task<EventDetailDTO> DetailAs(string? token, Guid eventId) {
        using var client = await ClientFor(token);
        return (await client.GetFromJsonAsync<EventDetailDTO>($"/api/events/{eventId}"))!;
    }

    [Test]
    public async Task Managed_list_shows_the_events_of_the_callers_chapters_and_the_chapters_below() {
        var stateEvent = Builder.SeedEvent(_state.Id, visibility: EventVisibility.MembersOnly);
        var districtEvent = Builder.SeedEvent(_district.Id, visibility: EventVisibility.Private);
        Builder.SeedEvent(_otherState.Id, visibility: EventVisibility.Public);
        var boardMember = SeedHolderOf(PermissionIdentifier.SystemRole.ChapterOfficer, _state.Id);
        var stateDelegate = SeedHolderOf(PermissionIdentifier.SystemRole.GeneralChapterDelegate, _state.Id);
        var (_, plainMember) = Builder.SeedAuthenticatedUser();

        await Assert.That(await ListAs(boardMember, true)).IsEquivalentTo(new[] { stateEvent.Id, districtEvent.Id });
        await Assert.That(await ListAs(stateDelegate, true)).IsEquivalentTo(new[] { stateEvent.Id });
        await Assert.That(await ListAs(plainMember, true)).IsEmpty();
    }

    [Test]
    public async Task Public_list_shows_private_events_only_of_the_callers_chapters() {
        var districtEvent = Builder.SeedEvent(_district.Id, visibility: EventVisibility.Private);
        var foreignPrivate = Builder.SeedEvent(_otherState.Id, visibility: EventVisibility.Private);
        var foreignPublic = Builder.SeedEvent(_otherState.Id, visibility: EventVisibility.Public);
        var boardMember = SeedHolderOf(PermissionIdentifier.SystemRole.ChapterOfficer, _state.Id);

        var listed = await ListAs(boardMember, false);

        await Assert.That(listed).Contains(districtEvent.Id);
        await Assert.That(listed).Contains(foreignPublic.Id);
        await Assert.That(listed).DoesNotContain(foreignPrivate.Id);
    }

    [Test]
    public async Task Public_list_carries_no_checklist_progress() {
        var ev = Builder.SeedEvent(_district.Id, visibility: EventVisibility.Public);
        Builder.SeedChecklistItem(ev.Id, isCompleted: true);
        using var client = AnonymousClient();

        var response = await client.GetFromJsonAsync<EventSearchResponse>("/api/events?page=1&pageSize=50");

        var listed = response!.Items.Single(e => e.Id == ev.Id);
        await Assert.That(listed.ChecklistTotal).IsEqualTo(0);
        await Assert.That(listed.ChecklistCompleted).IsEqualTo(0);
    }

    [Test]
    public async Task Detail_gives_internal_name_and_checklist_only_to_those_who_manage_the_event() {
        var ev = Builder.SeedEvent(_district.Id, internalName: "Intern", visibility: EventVisibility.Public);
        Builder.SeedChecklistItem(ev.Id);
        var boardMember = SeedHolderOf(PermissionIdentifier.SystemRole.ChapterOfficer, _state.Id);
        var (_, viewer) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [_district.Id] = new[] { PermissionIdentifier.ViewEvents } });
        var (_, plainMember) = Builder.SeedAuthenticatedUser();

        foreach (var outsider in new[] { null, plainMember }) {
            var view = await DetailAs(outsider, ev.Id);
            await Assert.That(view.CanManage).IsFalse();
            await Assert.That(view.InternalName).IsEqualTo("");
            await Assert.That(view.ChecklistItems).IsEmpty();
        }
        foreach (var manager in new[] { boardMember, viewer }) {
            var view = await DetailAs(manager, ev.Id);
            await Assert.That(view.CanManage).IsTrue();
            await Assert.That(view.InternalName).IsEqualTo("Intern");
            await Assert.That(view.ChecklistItems.Count).IsEqualTo(1);
        }
    }
}
