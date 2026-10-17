using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyAssistant;

public class AssemblyLogEndpointTests : IntegrationTestBase {
    private Guid _eventId;
    private string _viewToken = "";
    private string _chairToken = "";
    private string _plainToken = "";

    [Before(Test)]
    public void Setup() {
        var chapter = Builder.SeedChapter();
        _eventId = Builder.SeedEvent(chapter.Id).Id;
        (_, _viewToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.AssemblyView] });
        (_, _chairToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.AssemblyChair] });
        (_, _plainToken) = Builder.SeedAuthenticatedUser();
    }

    private AssemblySession SeedSession(bool ended) {
        var now = DateTime.UtcNow;
        var session = new AssemblySession { EventId = _eventId, StartedAt = now.AddHours(-2), EndedAt = ended ? now : null, StartedByUserId = Guid.NewGuid() };
        Db.Insert(session);
        Db.Insert(new AssemblyLogEntry { SessionId = session.Id, At = now.AddHours(-1), Kind = AssemblyLogKind.ChairAction, Detail = "ui.assembly_console.notice.list_closed" });
        return session;
    }

    [Test]
    public async Task Export_requires_view_permission() {
        var session = SeedSession(ended: true);

        var plain = await (await AuthenticatedClientWithCsrfAsync(_plainToken)).GetAsync($"/api/assembly/sessions/{session.Id}/log?format=csv");
        var viewer = await (await AuthenticatedClientWithCsrfAsync(_viewToken)).GetAsync($"/api/assembly/sessions/{session.Id}/log?format=csv");
        var chair = await (await AuthenticatedClientWithCsrfAsync(_chairToken)).GetAsync($"/api/assembly/sessions/{session.Id}/log?format=markdown");

        await Assert.That(plain.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(viewer.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var csv = await viewer.Content.ReadAsByteArrayAsync();
        await Assert.That(csv[0]).IsEqualTo((byte)0xEF);
        await Assert.That(Encoding.UTF8.GetString(csv)).Contains("Redeliste geschlossen");
        await Assert.That(chair.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(chair.Content.Headers.ContentType!.MediaType).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task Sessions_are_listed_with_entry_counts() {
        SeedSession(ended: true);
        SeedSession(ended: false);

        var sessions = await (await AuthenticatedClientWithCsrfAsync(_viewToken)).GetFromJsonAsync<List<AssemblySessionSummaryDTO>>($"/api/assembly/{_eventId}/sessions");

        await Assert.That(sessions!.Count).IsEqualTo(2);
        await Assert.That(sessions.All(s => s.EntryCount == 1)).IsTrue();
    }

    [Test]
    public async Task Long_item_title_and_name_are_cut_to_column_size() {
        var session = SeedSession(ended: true);
        var entry = new AssemblyLogEntry {
            SessionId = session.Id, At = DateTime.UtcNow, Kind = AssemblyLogKind.Speech,
            ItemTitle = new string('T', 2000), MumbleName = new string('N', 500)
        };

        new AssemblySessionRepository(Db).SaveLog([entry]);

        var stored = Db.AssemblyLogEntries.Single(e => e.Id == entry.Id);
        await Assert.That(stored.ItemTitle!.Length).IsEqualTo(AssemblyLogEntry.ItemTitleMaxLength);
        await Assert.That(stored.MumbleName!.Length).IsEqualTo(AssemblyLogEntry.MumbleNameMaxLength);
    }

    [Test]
    public async Task Delete_refused_for_running_session() {
        var session = SeedSession(ended: false);

        var response = await (await AuthenticatedClientWithCsrfAsync(_chairToken)).PostAsync($"/api/assembly/sessions/{session.Id}/log/delete", JsonContent.Create(new { }));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Db.AssemblyLogEntries.Count(e => e.SessionId == session.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Delete_removes_entries_of_ended_session_and_requires_chair() {
        var session = SeedSession(ended: true);

        var viewer = await (await AuthenticatedClientWithCsrfAsync(_viewToken)).PostAsync($"/api/assembly/sessions/{session.Id}/log/delete", JsonContent.Create(new { }));
        var chair = await (await AuthenticatedClientWithCsrfAsync(_chairToken)).PostAsync($"/api/assembly/sessions/{session.Id}/log/delete", JsonContent.Create(new { }));
        var download = await (await AuthenticatedClientWithCsrfAsync(_viewToken)).GetAsync($"/api/assembly/sessions/{session.Id}/log");

        await Assert.That(viewer.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(chair.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyLogEntries.Count(e => e.SessionId == session.Id)).IsEqualTo(0);
        await Assert.That(Db.AssemblySessions.Single(s => s.Id == session.Id).LogDeletedAt).IsNotNull();
        await Assert.That(download.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
