using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class OpenSlidesSubscriptionTests {
    [Test]
    public async Task Subscription_follows_polls_agenda_and_countdown() {
        using var document = JsonDocument.Parse(OpenSlidesSubscription.Build(3));

        var meeting = document.RootElement[0];
        await Assert.That(meeting.GetProperty("collection").GetString()).IsEqualTo("meeting");
        await Assert.That(meeting.GetProperty("ids")[0].GetInt32()).IsEqualTo(3);

        var fields = meeting.GetProperty("fields");
        var polls = fields.GetProperty("poll_ids");
        await Assert.That(polls.GetProperty("type").GetString()).IsEqualTo("relation-list");
        await Assert.That(polls.GetProperty("collection").GetString()).IsEqualTo("poll");
        var pollFields = polls.GetProperty("fields").EnumerateObject().Select(p => p.Name).ToList();
        await Assert.That(pollFields).Contains("live_votes");
        await Assert.That(pollFields).Contains("votesvalid");
        await Assert.That(polls.GetProperty("fields").GetProperty("option_ids").GetProperty("collection").GetString()).IsEqualTo("option");

        var agenda = fields.GetProperty("agenda_item_ids").GetProperty("fields").GetProperty("content_object_id");
        await Assert.That(agenda.GetProperty("type").GetString()).IsEqualTo("generic-relation");
        await Assert.That(fields.GetProperty("poll_countdown_id").GetProperty("collection").GetString()).IsEqualTo("projector_countdown");
    }
}
