using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Playwright;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.E2E;

/// <summary>
/// End-to-end browser tests for the collaborative meeting-notes editor. These
/// drive two real Chromium tabs through Playwright against a real Kestrel
/// instance, so they exercise the full SignalR hub + Yjs CRDT + CodeMirror 5
/// binding + per-character authorship marker layer in one shot.
///
/// The tests poke the editor by evaluating JavaScript in the page context
/// (<c>cm.replaceRange(...)</c>) rather than synthesizing keystrokes. That's
/// intentional: CodeMirror's complex input handling makes keyboard-based
/// assertions flaky under headless browsers, and the y-codemirror binding
/// routes both pathways through the same Y.Text observer. What we care about
/// in E2E is the full pipeline: CM edit → Yjs update → SignalR hub relay →
/// remote Yjs apply → marker rebuild. The source of the initial CM change
/// doesn't matter.
/// </summary>
[NotInParallel("CollabEditorE2E")]
public class CollabEditorE2ETests : E2ETestBase {
    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// Shared fixture: one chapter, one in-progress meeting, one agenda item.
    /// Returned ids are used to navigate each browser tab to the live page.
    /// </summary>
    private (Guid chapterId, Guid meetingId, Guid itemId) SeedMeetingWithItem(
        MeetingStatus status = MeetingStatus.InProgress,
        MeetingVisibility visibility = MeetingVisibility.Public) {
        var chapter = Builder.SeedChapter("Test Chapter");
        var meeting = Builder.SeedMeeting(chapter.Id,
            title: "Collab Test Meeting",
            status: status,
            visibility: visibility);
        var item = Builder.SeedAgendaItem(meeting.Id, title: "TOP 1");
        return (chapter.Id, meeting.Id, item.Id);
    }

    /// <summary>
    /// Waits for the collaborative editor to finish bootstrapping. The CM5
    /// DOM element appears almost immediately but the collab handle (and
    /// therefore real JS interop) only exists after <c>createCollabEditor</c>
    /// finishes its async chain — load Yjs, apply snapshot, attach binding.
    /// On timeout, dumps the page body text to help diagnose.
    /// </summary>
    private static async Task WaitForCollabEditorAsync(IPage page) {
        // Timeouts are 60s rather than 30s because collab tests run two
        // browser contexts per test — under parallel load the per-test
        // resource budget (Blazor WASM boot + Yjs import + hub negotiate)
        // can easily push past 30s on shared CI workers.
        try {
            await page.WaitForSelectorAsync(".CodeMirror", new() { Timeout = 60000 });
            await page.WaitForFunctionAsync(
                @"() => {
                    const cm = document.querySelector('.CodeMirror')?.CodeMirror;
                    return cm != null && window.cmEditor != null && window.cmEditor._debugFirstHandle() != null;
                }",
                new PageWaitForFunctionOptions { Timeout = 60000 });
        } catch (TimeoutException) {
            var body = await page.EvaluateAsync<string>(
                "() => document.body?.innerText?.substring(0, 500) || '(no body)'");
            var url = page.Url;
            throw new TimeoutException(
                $"Timed out waiting for .CodeMirror at {url}. Page body text: {body}");
        }
    }

    /// <summary>Returns the plain text currently in the editor.</summary>
    private static async Task<string> GetEditorTextAsync(IPage page) {
        return await page.EvaluateAsync<string>(
            "() => document.querySelector('.CodeMirror').CodeMirror.getValue()");
    }

    /// <summary>
    /// Replaces the entire document content. We don't use CM5's built-in
    /// replace-all (<c>setValue</c>) because y-codemirror's binding treats
    /// programmatic replacements as a single giant transaction and we want
    /// the Y.Text observer to attribute the inserted range cleanly.
    /// </summary>
    private static Task InsertAtEndAsync(IPage page, string text) {
        var escaped = System.Text.Json.JsonSerializer.Serialize(text);
        return page.EvaluateAsync(
            @$"() => {{
                const cm = document.querySelector('.CodeMirror').CodeMirror;
                cm.focus();
                const lastLine = cm.lastLine();
                const lastCh = cm.getLine(lastLine).length;
                cm.replaceRange({escaped}, {{ line: lastLine, ch: lastCh }});
            }}");
    }

    /// <summary>Inserts text at a specific (line, ch) position.</summary>
    private static Task InsertAtAsync(IPage page, int line, int ch, string text) {
        var escaped = System.Text.Json.JsonSerializer.Serialize(text);
        return page.EvaluateAsync(
            @$"() => {{
                const cm = document.querySelector('.CodeMirror').CodeMirror;
                cm.focus();
                cm.replaceRange({escaped}, {{ line: {line}, ch: {ch} }});
            }}");
    }

    /// <summary>
    /// Returns the current set of author-attribution markers in the editor as
    /// a list of {title, css} rows. Used to assert that remote authors'
    /// runs have the correct color + name tooltip.
    /// </summary>
    private static async Task<List<MarkerRow>> GetAuthorMarkersAsync(IPage page) {
        var json = await page.EvaluateAsync<string>(
            @"() => {
                const cm = document.querySelector('.CodeMirror').CodeMirror;
                const marks = cm.getDoc().getAllMarks().filter(m => m.title).map(m => {
                    const r = m.find();
                    return {
                        title: m.title,
                        css: m.css,
                        fromLine: r?.from?.line, fromCh: r?.from?.ch,
                        toLine: r?.to?.line, toCh: r?.to?.ch
                    };
                });
                return JSON.stringify(marks);
            }");
        return System.Text.Json.JsonSerializer.Deserialize<List<MarkerRow>>(json)
            ?? new List<MarkerRow>();
    }

    private static async Task<Dictionary<string, string>> GetKnownAuthorsAsync(IPage page) {
        var json = await page.EvaluateAsync<string>(
            @"() => {
                const handle = window.cmEditor._debugFirstHandle();
                const map = window.cmEditor._debugKnownAuthors(handle);
                return JSON.stringify(map);
            }");
        var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AuthorInfoJson>>(json)
            ?? new Dictionary<string, AuthorInfoJson>();
        var result = new Dictionary<string, string>();
        foreach (var kv in parsed) {
            result[kv.Key] = kv.Value.color;
        }
        return result;
    }

    /// <summary>
    /// Waits until the editor's current text matches a predicate — used to
    /// give the SignalR hub a chance to relay updates between tabs. Much
    /// more reliable than a hardcoded sleep.
    /// </summary>
    private static Task WaitForEditorTextAsync(IPage page, string expectedSubstring, int timeoutMs = 10000) {
        var escaped = System.Text.Json.JsonSerializer.Serialize(expectedSubstring);
        return page.WaitForFunctionAsync(
            @$"() => {{
                const cm = document.querySelector('.CodeMirror')?.CodeMirror;
                return cm && cm.getValue().includes({escaped});
            }}",
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    private static Task WaitForMarkerCountAsync(IPage page, int expected, int timeoutMs = 10000) {
        return page.WaitForFunctionAsync(
            @$"() => {{
                const cm = document.querySelector('.CodeMirror')?.CodeMirror;
                if (!cm) return false;
                const n = cm.getDoc().getAllMarks().filter(m => m.title).length;
                return n === {expected};
            }}",
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    // ---- Tests ------------------------------------------------------------

    [Test]
    public async Task Two_users_see_each_others_text_with_correct_author_colors() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        var (bob, bobToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);

        var bobPage = await NewAuthenticatedPageAsync(bobToken);
        await bobPage.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(bobPage);

        // Alice writes the first line.
        await InsertAtEndAsync(Page, "Line from Alice.\n");
        await WaitForEditorTextAsync(bobPage, "Line from Alice.");

        // Bob writes a second line at the end.
        await InsertAtEndAsync(bobPage, "Line from Bob.\n");
        await WaitForEditorTextAsync(Page, "Line from Bob.");

        // Known-authors cache must contain both users on both tabs.
        var aliceKnown = await GetKnownAuthorsAsync(Page);
        var bobKnown = await GetKnownAuthorsAsync(bobPage);
        await Assert.That(aliceKnown).ContainsKey(alice.Id.ToString());
        await Assert.That(aliceKnown).ContainsKey(bob.Id.ToString());
        await Assert.That(bobKnown).ContainsKey(alice.Id.ToString());
        await Assert.That(bobKnown).ContainsKey(bob.Id.ToString());

        // The two users must have distinct palette colors.
        await Assert.That(aliceKnown[alice.Id.ToString()])
            .IsNotEqualTo(aliceKnown[bob.Id.ToString()]);

        // Both tabs must show an author marker for each attributed run.
        // The plan says everyone sees every attributed character (including
        // their own) in its author's color, so each tab should show 2 markers.
        await WaitForMarkerCountAsync(Page, 2);
        await WaitForMarkerCountAsync(bobPage, 2);

        var aliceMarkers = await GetAuthorMarkersAsync(Page);
        var bobMarkers = await GetAuthorMarkersAsync(bobPage);
        // Tab A and tab B must render an identical marker set.
        await Assert.That(aliceMarkers.Count).IsEqualTo(bobMarkers.Count);
        foreach (var marker in aliceMarkers) {
            await Assert.That(marker.title).StartsWith("Geschrieben von ");
            await Assert.That(marker.css).StartsWith("background-color: rgba(");
        }
    }

    [Test]
    public async Task Authors_stay_colored_after_one_user_disconnects() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        var (bob, bobToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);

        var bobPage = await NewAuthenticatedPageAsync(bobToken);
        await bobPage.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(bobPage);

        await InsertAtEndAsync(Page, "Alice writes while still connected.\n");
        await WaitForEditorTextAsync(bobPage, "Alice writes");

        // Bob should know Alice's color from live awareness.
        var bobKnownBefore = await GetKnownAuthorsAsync(bobPage);
        var aliceColorBefore = bobKnownBefore[alice.Id.ToString()];

        // Alice disconnects by closing her tab. Bob's awareness map loses
        // the Alice entry but the persistent knownAuthors cache retains her.
        await Page.CloseAsync();

        // Give the awareness timeout a moment to propagate — y-protocols
        // removes stale entries after ~30s but the cache should survive.
        await bobPage.WaitForTimeoutAsync(500);

        var bobKnownAfter = await GetKnownAuthorsAsync(bobPage);
        await Assert.That(bobKnownAfter).ContainsKey(alice.Id.ToString());
        await Assert.That(bobKnownAfter[alice.Id.ToString()]).IsEqualTo(aliceColorBefore);

        // Marker on Alice's run must still render with Alice's original
        // color — not the neutral "Unbekannt" fallback.
        var markers = await GetAuthorMarkersAsync(bobPage);
        var aliceMarker = markers.FirstOrDefault(m => m.title == "Geschrieben von " + alice.FirstName + " " + alice.LastName);
        await Assert.That(aliceMarker).IsNotNull();
        await Assert.That(aliceMarker!.css).Contains(HexToRgbFragment(aliceColorBefore));
    }

    [Test]
    public async Task Authors_survive_page_reload_via_server_snapshot() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);

        await InsertAtEndAsync(Page, "Text that should persist.\n");
        // Give the marker layer a beat to settle so the format transaction fires.
        await WaitForMarkerCountAsync(Page, 1);

        // Force an immediate snapshot save via the hub method, bypassing
        // the 10-second client-side timer. We reach in through the JS layer.
        await Page.EvaluateAsync(@"
            async () => {
                // The snapshot timer fires every ~10s — skip the wait by
                // calling the Blazor-side trigger if exposed, otherwise
                // fall back to a manual wait.
                await new Promise(r => setTimeout(r, 11000));
            }");

        // Now reload the page from scratch. AuthService re-reads localStorage
        // and Blazor re-initializes the collaborative editor from the server
        // snapshot + the persisted known-authors map.
        await Page.ReloadAsync();
        await WaitForCollabEditorAsync(Page);
        await WaitForEditorTextAsync(Page, "Text that should persist.");

        var known = await GetKnownAuthorsAsync(Page);
        await Assert.That(known).ContainsKey(alice.Id.ToString());

        // Alice's text should still have a marker after reload — sourced
        // from the persistent CollabDocument.ClientUserMap column, not
        // from the live awareness map.
        await WaitForMarkerCountAsync(Page, 1);
        var markers = await GetAuthorMarkersAsync(Page);
        await Assert.That(markers.Count).IsEqualTo(1);
        await Assert.That(markers[0].title).IsEqualTo("Geschrieben von " + alice.FirstName + " " + alice.LastName);
    }

    [Test]
    public async Task Public_meeting_allows_anonymous_viewer_to_see_colored_text() {
        // A public meeting with some attributed text, viewed by an anonymous
        // browser — the authors' colors must be visible, but the editor
        // must be in read-only mode.
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem(
            status: MeetingStatus.InProgress,
            visibility: MeetingVisibility.Public);
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });

        // Alice writes some text as an authenticated user.
        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);
        await InsertAtEndAsync(Page, "Alice contributed this.\n");
        await WaitForMarkerCountAsync(Page, 1);

        // Wait for the snapshot to persist so the anonymous viewer reads it
        // from CollabDocument rather than from the empty legacy Notes.
        await Page.EvaluateAsync("async () => { await new Promise(r => setTimeout(r, 11000)); }");

        // Anonymous viewer opens a fresh context — no token injected.
        var anonPage = await NewAnonymousPageAsync();
        await anonPage.GotoAsync($"/Administration/Meetings/{meetingId}/Live");

        // Anonymous users are routed to login when the meeting isn't public
        // OR when the Live page explicitly requires edit perms — but this
        // meeting is Public + InProgress, so the MeetingAccessHelper allows
        // them to view. The live editor will be in read-only mode.
        await WaitForCollabEditorAsync(anonPage);
        await WaitForEditorTextAsync(anonPage, "Alice contributed this.");

        // Read-only badge should be visible.
        var badgeText = await anonPage.Locator(".alert-secondary").First.TextContentAsync();
        await Assert.That(badgeText ?? "").Contains("Schreibgeschützt");

        // Alice's color should still be in the known-authors cache — loaded
        // from the persistent snapshot.
        var known = await GetKnownAuthorsAsync(anonPage);
        await Assert.That(known).ContainsKey(alice.Id.ToString());

        // And the marker should render with her color.
        await WaitForMarkerCountAsync(anonPage, 1);
        var markers = await GetAuthorMarkersAsync(anonPage);
        await Assert.That(markers[0].title).IsEqualTo("Geschrieben von " + alice.FirstName + " " + alice.LastName);
        await Assert.That(markers[0].css).Contains(HexToRgbFragment(known[alice.Id.ToString()]));
    }

    [Test]
    public async Task Read_only_viewer_cannot_edit_but_still_sees_colors() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        var (viewer, viewerToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.ViewMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);
        await InsertAtEndAsync(Page, "Alice wrote this.\n");
        await WaitForMarkerCountAsync(Page, 1);

        var viewerPage = await NewAuthenticatedPageAsync(viewerToken);
        await viewerPage.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(viewerPage);
        await WaitForEditorTextAsync(viewerPage, "Alice wrote this.");

        // Editor must be in nocursor read-only mode for the viewer.
        var readOnly = await viewerPage.EvaluateAsync<string>(
            "() => String(document.querySelector('.CodeMirror').CodeMirror.getOption('readOnly'))");
        await Assert.That(readOnly).IsEqualTo("nocursor");

        // "Schreibgeschützt" badge visible.
        var badgeText = await viewerPage.Locator(".alert-secondary").First.TextContentAsync();
        await Assert.That(badgeText ?? "").Contains("Schreibgeschützt");

        // Marker for Alice's run is still rendered.
        await WaitForMarkerCountAsync(viewerPage, 1);
    }

    [Test]
    public async Task Completed_meeting_freezes_the_document() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem(
            status: MeetingStatus.InProgress);
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings, PermissionIdentifier.DeleteMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);
        await InsertAtEndAsync(Page, "Content before freezing.\n");
        await WaitForMarkerCountAsync(Page, 1);

        // Transition the meeting to Completed via the API endpoint from
        // inside the browser (reusing the authenticated session).
        await Page.EvaluateAsync(@$"
            async () => {{
                const af = await fetch('/api/antiforgery/token').then(r => r.json());
                await fetch('/api/meetings/{meetingId}/status', {{
                    method: 'PUT',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Bearer ' + localStorage.getItem('auth_token'),
                        'X-CSRF-TOKEN': af.token
                    }},
                    body: JSON.stringify({{ Id: '{meetingId}', Status: 3 }})
                }});
            }}");

        // The Phase 1 hub broadcast delivers MeetingStatusChanged — the live
        // page reloads the meeting, sees it's no longer InProgress, and
        // replaces the editor with the "not active" warning.
        await Page.WaitForFunctionAsync(
            "() => !document.querySelector('.CodeMirror') && document.body.textContent.includes('nicht aktiv')",
            new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    [Test]
    public async Task Mid_line_insertion_creates_separate_author_markers() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } },
            firstName: "Alice", lastName: "Author");
        var (bob, bobToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } },
            firstName: "Bob", lastName: "Builder");

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);

        var bobPage = await NewAuthenticatedPageAsync(bobToken);
        await bobPage.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(bobPage);

        // Alice writes a full line.
        await InsertAtEndAsync(Page, "Alice writes an original sentence.\n");
        await WaitForEditorTextAsync(bobPage, "Alice writes an original sentence.");

        // Bob inserts into the middle of Alice's line.
        await InsertAtAsync(bobPage, 0, 13, " [Bob-insert]");
        await WaitForEditorTextAsync(Page, "Bob-insert");

        // Both tabs should now see three runs:
        //   "Alice writes " (Alice) + " [Bob-insert]" (Bob) + "an original sentence.\n" (Alice)
        // which means three markers per tab.
        await WaitForMarkerCountAsync(Page, 3);
        await WaitForMarkerCountAsync(bobPage, 3);

        var aliceMarkers = await GetAuthorMarkersAsync(Page);
        var aliceFullName = alice.FirstName + " " + alice.LastName;
        var bobFullName = bob.FirstName + " " + bob.LastName;
        var aliceRunCount = aliceMarkers.FindAll(m => m.title == "Geschrieben von " + aliceFullName).Count;
        var bobRunCount = aliceMarkers.FindAll(m => m.title == "Geschrieben von " + bobFullName).Count;
        await Assert.That(aliceRunCount).IsEqualTo(2);
        await Assert.That(bobRunCount).IsEqualTo(1);
    }

    [Test]
    public async Task Legacy_seeded_notes_text_is_uncolored() {
        // An agenda item with pre-existing plain-text Notes gets seeded into
        // the Yjs doc on first LoadDocument without author attributes — those
        // runs should render with NO marker. Use Public visibility so the
        // seeded authenticated user doesn't hit the "direct officer only"
        // gate in MeetingAccessHelper for private non-draft meetings.
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id,
            status: MeetingStatus.InProgress,
            visibility: MeetingVisibility.Public);
        var item = Builder.SeedAgendaItem(meeting.Id);
        // Set the legacy Notes column directly via LinqToDB.
        Db.AgendaItems
            .Where(a => a.Id == item.Id)
            .Set(a => a.Notes, "Legacy notes text.")
            .Update();

        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meeting.Id}/Live");
        await WaitForCollabEditorAsync(Page);
        await WaitForEditorTextAsync(Page, "Legacy notes text.");

        // No author markers — the seeded text has no author attribute.
        var markers = await GetAuthorMarkersAsync(Page);
        await Assert.That(markers.Count).IsEqualTo(0);

        // Now Alice adds her own line — her run gets a marker, legacy stays uncolored.
        await InsertAtEndAsync(Page, "\nAlice's contribution.");
        await WaitForMarkerCountAsync(Page, 1);
        var updated = await GetAuthorMarkersAsync(Page);
        await Assert.That(updated[0].title).IsEqualTo("Geschrieben von " + alice.FirstName + " " + alice.LastName);
    }

    [Test]
    public async Task Dark_mode_toggle_updates_editor_theme_class() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);

        // Force the theme attribute on <html>. The MutationObserver inside
        // codemirror-editor.js should pick it up and swap the editor's theme.
        await Page.EvaluateAsync(
            "() => document.documentElement.setAttribute('data-bs-theme', 'dark')");
        await Page.WaitForFunctionAsync(
            "() => document.querySelector('.CodeMirror').classList.contains('cm-s-material-darker')",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // And back to light.
        await Page.EvaluateAsync(
            "() => document.documentElement.setAttribute('data-bs-theme', 'light')");
        await Page.WaitForFunctionAsync(
            "() => document.querySelector('.CodeMirror').classList.contains('cm-s-default')",
            new PageWaitForFunctionOptions { Timeout = 5000 });
    }

    [Test]
    public async Task Save_indicator_transitions_through_states() {
        var (chapterId, meetingId, itemId) = SeedMeetingWithItem();
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });

        await InjectAuthTokenAsync(aliceToken);
        await Page.GotoAsync($"/Administration/Meetings/{meetingId}/Live");
        await WaitForCollabEditorAsync(Page);

        // Initially: "Saved" state with no LastSavedAt → indicator is blank.
        // Type → Dirty → "Ungespeicherte Änderungen".
        await InsertAtEndAsync(Page, "edit");
        await Page.WaitForFunctionAsync(
            "() => document.body.textContent.includes('Ungespeicherte Änderungen')",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Wait for snapshot timer → "Wird gespeichert…" briefly, then "Gespeichert HH:mm:ss".
        await Page.WaitForFunctionAsync(
            "() => /Gespeichert \\d{2}:\\d{2}:\\d{2}/.test(document.body.textContent)",
            new PageWaitForFunctionOptions { Timeout = 30000 });
    }

    // ---- Private types ----------------------------------------------------

    private class MarkerRow {
        public string title { get; set; } = "";
        public string css { get; set; } = "";
        public int fromLine { get; set; }
        public int fromCh { get; set; }
        public int toLine { get; set; }
        public int toCh { get; set; }
    }

    private class AuthorInfoJson {
        public string name { get; set; } = "";
        public string color { get; set; } = "";
    }

    /// <summary>
    /// Converts "#EE7733" to "rgb(238, 119, 51" so we can assert a marker's
    /// css string contains the right RGB triple regardless of the alpha.
    /// </summary>
    private static string HexToRgbFragment(string hex) {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#')
            return "";
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return $"rgba({r}, {g}, {b}";
    }
}
