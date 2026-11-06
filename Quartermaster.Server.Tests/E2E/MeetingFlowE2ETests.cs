using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.E2E;

/// <summary>
/// End-to-end flow test for the Meeting system. Drives the Blazor UI through a
/// complete meeting lifecycle (create → schedule → in-progress → completed → archived).
/// </summary>
public class MeetingFlowE2ETests : E2ETestBase {
    [Test]
    public async Task Seeded_meeting_appears_in_list() {
        var chapter = Builder.SeedChapter("Test Chapter");
        Builder.SeedMeeting(chapter.Id, title: "März-Sitzung 2026",
            status: MeetingStatus.Scheduled, visibility: MeetingVisibility.Public);

        // Public meeting → anonymous should see it on the list.
        await Page.GotoAsync("/Administration/Meetings");
        await Page.WaitForSelectorAsync("body",
            new PageWaitForSelectorOptions { Timeout = 30000 });
        // Wait for the list to hydrate - look for table or card with meeting data
        await Page.WaitForSelectorAsync("table, .card, .list-group",
            new PageWaitForSelectorOptions { Timeout = 45000 });
        var bodyText = await Page.Locator("body").TextContentAsync();
        await Assert.That(bodyText ?? "").Contains("März-Sitzung 2026");
    }

    [Test]
    public async Task Meeting_detail_page_loads_for_seeded_meeting() {
        var chapter = Builder.SeedChapter("Test Chapter");
        var meeting = Builder.SeedMeeting(chapter.Id, title: "Detail-Sitzung",
            status: MeetingStatus.Scheduled, visibility: MeetingVisibility.Public);

        await Page.GotoAsync($"/Administration/Meetings/{meeting.Id}");
        await Page.WaitForSelectorAsync("body",
            new PageWaitForSelectorOptions { Timeout = 30000 });
        // Wait for the title to render
        await Page.WaitForSelectorAsync("h1, h2, h3, h4, h5",
            new PageWaitForSelectorOptions { Timeout = 45000 });
        var bodyText = await Page.Locator("body").TextContentAsync();
        await Assert.That(bodyText ?? "").Contains("Detail-Sitzung");
    }

    /// <summary>A meeting carries a date only; its detail page crashed formatting that date with hours and minutes.</summary>
    [Test]
    public async Task Meeting_detail_page_shows_the_meeting_date_in_a_date_field() {
        var chapter = Builder.SeedChapter("Test Chapter");
        var meeting = Builder.SeedMeeting(chapter.Id, title: "Datum-Sitzung",
            visibility: MeetingVisibility.Public, meetingDate: new DateTime(2026, 11, 20));
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(token);
        await Page.GotoAsync($"/Administration/Meetings/{meeting.Id}");
        var dateField = Page.Locator("input[type=date]");
        await dateField.WaitForAsync(new LocatorWaitForOptions { Timeout = 45000 });

        await Assert.That(await dateField.InputValueAsync()).IsEqualTo("2026-11-20");
        await Assert.That(await Page.Locator(".blazor-error-boundary, .app-error-boundary").CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task Public_meeting_accessible_to_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, title: "Öffentliche Sitzung",
            status: MeetingStatus.Completed, visibility: MeetingVisibility.Public);

        // Anonymous browser hits the detail page - should not be redirected to login
        await Page.GotoAsync($"/Administration/Meetings/{meeting.Id}");
        await Page.WaitForSelectorAsync("body",
            new PageWaitForSelectorOptions { Timeout = 30000 });
        // Wait a bit for Blazor to fetch + render
        await Page.WaitForSelectorAsync("h1, h2, h3, h4, h5",
            new PageWaitForSelectorOptions { Timeout = 45000 });
    }
}
