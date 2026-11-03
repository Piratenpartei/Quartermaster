using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

/// <summary>Checklist mail and motion targets must stay inside the event's chapter, like chapter mails.</summary>
public class ChecklistTargetReachTests : IntegrationTestBase {
    private Chapter _root = default!;
    private Chapter _local = default!;
    private Chapter _localChild = default!;
    private Event _event = default!;

    [Before(Test)]
    public void SeedTree() {
        _root = Builder.SeedChapter("Bund");
        _local = Builder.SeedChapter("Ort", parentChapterId: _root.Id);
        _localChild = Builder.SeedChapter("Ortsteil", parentChapterId: _local.Id);
        _event = Builder.SeedEvent(_local.Id);
        Builder.SeedMember(chapterId: _root.Id, firstName: "Fremd", lastName: "Person", email: "fremd@test.local");
        Builder.SeedMember(chapterId: _localChild.Id, firstName: "Eigen", lastName: "Person", email: "eigen@test.local");
    }

    private async Task<HttpClient> LocalOfficerAsync() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [_local.Id] = new[] { PermissionIdentifier.EditEvents } });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private static EventChecklistItemConfigDTO MailTo(string targetType, Guid targetId) {
        return new EventChecklistItemConfigDTO { TargetType = targetType, TargetId = targetId };
    }

    private Task<HttpResponseMessage> Resolve(HttpClient client, EventChecklistItemConfigDTO config) {
        return client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist/resolve-recipients",
            new RecipientPreviewRequest { EventId = _event.Id, Configuration = config });
    }

    private EventChecklistItem SeedConfiguredItem(ChecklistItemType type, EventChecklistItemConfigDTO config) {
        var item = Builder.SeedChecklistItem(_event.Id, itemType: type);
        Db.EventChecklistItems.Where(i => i.Id == item.Id)
            .Set(i => i.Configuration, EventConfigSerializer.Serialize(config))
            .Update();
        return item;
    }

    [Test]
    public async Task Resolve_refuses_a_chapter_above_the_event_chapter() {
        using var client = await LocalOfficerAsync();

        var response = await Resolve(client, MailTo("Chapter", _root.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).DoesNotContain("fremd@test.local");
    }

    [Test]
    public async Task Resolve_lists_members_below_the_event_chapter() {
        using var client = await LocalOfficerAsync();

        var response = await Resolve(client, MailTo("Chapter", _local.Id));
        var preview = await response.Content.ReadFromJsonAsync<RecipientPreviewResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(preview!.Recipients.Select(r => r.Email).ToList()).IsEquivalentTo(new[] { "eigen@test.local" });
    }

    [Test]
    public async Task Resolve_refuses_a_division_the_event_chapter_does_not_cover() {
        var foreignDivision = Builder.SeedDivisionFor(_root.Id);
        using var client = await LocalOfficerAsync();

        var response = await Resolve(client, MailTo("AdministrativeDivision", foreignDivision));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>Manual addresses are typed in by the officer and reach no member list, so they are not tied to the chapter tree, even with a target id left over from an earlier choice.</summary>
    [Test]
    public async Task Manual_addresses_are_not_checked_against_the_chapter_tree() {
        Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Description, "Einladung").Update();
        var config = new EventChecklistItemConfigDTO {
            TargetType = "ManualAddresses",
            ManualAddresses = "extern@example.org",
            TargetId = _root.Id,
            UseDescription = true
        };
        using var client = await LocalOfficerAsync();

        var preview = await Resolve(client, config);
        var added = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist", new ChecklistItemCreateRequest {
            EventId = _event.Id,
            Label = "Einladung",
            ItemType = ChecklistItemType.SendEmail,
            Configuration = config
        });
        var item = await added.Content.ReadFromJsonAsync<EventChecklistItemDTO>();
        var executed = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist/{item!.Id}/check",
            new ChecklistItemCheckRequest { EventId = _event.Id, ItemId = item.Id, ExecuteAction = true });

        await Assert.That(preview.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await preview.Content.ReadFromJsonAsync<RecipientPreviewResponse>())!.Recipients.Single().Email).IsEqualTo("extern@example.org");
        await Assert.That(added.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(executed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.EventChecklistItems.Single(i => i.Id == item.Id).IsCompleted).IsTrue();
    }

    [Test]
    public async Task Add_refuses_a_mail_to_a_chapter_above_the_event_chapter() {
        using var client = await LocalOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist", new ChecklistItemCreateRequest {
            EventId = _event.Id,
            Label = "Einladung",
            ItemType = ChecklistItemType.SendEmail,
            Configuration = MailTo("Chapter", _root.Id)
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Event.Checklist.TargetOutOfReach);
        await Assert.That(Db.EventChecklistItems.Count(i => i.EventId == _event.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Add_refuses_a_motion_in_another_chapter() {
        using var client = await LocalOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist", new ChecklistItemCreateRequest {
            EventId = _event.Id,
            Label = "Antrag",
            ItemType = ChecklistItemType.CreateMotion,
            Configuration = new EventChecklistItemConfigDTO { ChapterId = _root.Id, MotionTitle = "T", MotionText = "Text" }
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Add_accepts_a_motion_in_a_chapter_below() {
        using var client = await LocalOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist", new ChecklistItemCreateRequest {
            EventId = _event.Id,
            Label = "Antrag",
            ItemType = ChecklistItemType.CreateMotion,
            Configuration = new EventChecklistItemConfigDTO { ChapterId = _localChild.Id, MotionTitle = "T", MotionText = "Text" }
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Update_refuses_a_mail_to_a_chapter_above_the_event_chapter() {
        var item = SeedConfiguredItem(ChecklistItemType.SendEmail, MailTo("Chapter", _local.Id));
        using var client = await LocalOfficerAsync();

        var response = await client.PutAsJsonAsync($"/api/events/{_event.Id}/checklist/{item.Id}", new ChecklistItemUpdateRequest {
            EventId = _event.Id,
            ItemId = item.Id,
            Label = "Einladung",
            ItemType = ChecklistItemType.SendEmail,
            Configuration = MailTo("Chapter", _root.Id)
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var stored = EventConfigSerializer.ParseConfig(Db.EventChecklistItems.Single(i => i.Id == item.Id).Configuration);
        await Assert.That(stored!.TargetId).IsEqualTo(_local.Id);
    }

    [Test]
    public async Task Executing_a_stored_motion_item_for_another_chapter_creates_nothing() {
        var item = SeedConfiguredItem(ChecklistItemType.CreateMotion,
            new EventChecklistItemConfigDTO { ChapterId = _root.Id, MotionTitle = "T", MotionText = "Text" });
        using var client = await LocalOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist/{item.Id}/check",
            new ChecklistItemCheckRequest { EventId = _event.Id, ItemId = item.Id, ExecuteAction = true });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.Motions.Count(m => m.ChapterId == _root.Id)).IsEqualTo(0);
        await Assert.That(Db.EventChecklistItems.Single(i => i.Id == item.Id).IsCompleted).IsFalse();
    }

    [Test]
    public async Task Executing_a_stored_mail_item_for_a_chapter_above_sends_nothing() {
        Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Description, "Einladung").Update();
        var config = MailTo("Chapter", _root.Id);
        config.UseDescription = true;
        var item = SeedConfiguredItem(ChecklistItemType.SendEmail, config);
        using var client = await LocalOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_event.Id}/checklist/{item.Id}/check",
            new ChecklistItemCheckRequest { EventId = _event.Id, ItemId = item.Id, ExecuteAction = true });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.NotificationLogs.Count(l => l.Recipient == "fremd@test.local")).IsEqualTo(0);
    }
}
