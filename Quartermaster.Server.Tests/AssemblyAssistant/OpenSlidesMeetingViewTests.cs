using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class OpenSlidesMeetingViewTests {
    private static OpenSlidesMeetingView View() {
        var store = new OpenSlidesKeyStore();
        using var document = JsonDocument.Parse(OpenSlidesFixtures.FullMap);
        store.Apply(document.RootElement);
        return OpenSlidesMeetingView.From(store, OpenSlidesFixtures.MeetingId);
    }

    [Test]
    public async Task Polls_are_read_with_decimal_strings() {
        var view = View();

        var motionPoll = view.Polls.Single(p => p.Id == 7);
        await Assert.That(motionPoll.State).IsEqualTo("finished");
        await Assert.That(motionPoll.ContentObjectId).IsEqualTo("motion/12");
        await Assert.That(motionPoll.VotesValid).IsEqualTo(71m);
        var option = motionPoll.Options.Single();
        await Assert.That(option.Yes).IsEqualTo(34m);
        await Assert.That(option.No).IsEqualTo(17m);
        await Assert.That(option.Abstain).IsEqualTo(20m);
        await Assert.That(motionPoll.GlobalOption!.Id).IsEqualTo(71);
    }

    [Test]
    public async Task Live_vote_count_counts_keys() {
        var view = View();

        var topicPoll = view.Polls.Single(p => p.Id == 8);
        await Assert.That(topicPoll.LiveVoteCount).IsEqualTo(3);
        await Assert.That(topicPoll.Options.Select(o => o.Text)).IsEquivalentTo(["Ja", "Nein"]);
    }

    [Test]
    public async Task Agenda_item_has_motion_number() {
        var view = View();

        var motionItem = view.AgendaItems.Single(a => a.Id == 20);
        await Assert.That(motionItem.MotionNumber).IsEqualTo("SÄA001");
        await Assert.That(motionItem.Title).IsEqualTo("Änderung der Geschäftsordnung");
        await Assert.That(motionItem.ItemNumber).IsEqualTo("4.1");
        var topicItem = view.AgendaItems.Single(a => a.Id == 21);
        await Assert.That(topicItem.ParentId).IsEqualTo(20);
        await Assert.That(topicItem.MotionNumber).IsNull();
    }

    [Test]
    public async Task Countdown_is_read() {
        var countdown = View().PollCountdown!;

        await Assert.That(countdown.Id).IsEqualTo(2);
        await Assert.That(countdown.Running).IsTrue();
        await Assert.That(countdown.CountdownTime).IsEqualTo(1800000070.5);
        await Assert.That(countdown.DefaultTime).IsEqualTo(60);
    }

    [Test]
    public async Task Empty_store_gives_empty_view() {
        var view = OpenSlidesMeetingView.From(new OpenSlidesKeyStore(), 3);

        await Assert.That(view.Polls).IsEmpty();
        await Assert.That(view.PollCountdown).IsNull();
    }
}
