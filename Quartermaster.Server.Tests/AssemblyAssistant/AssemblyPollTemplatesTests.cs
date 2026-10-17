using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Templates;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class AssemblyPollTemplatesTests {
    private static OpenSlidesMeetingView View(bool withProceduralTopic = true) {
        var store = new OpenSlidesKeyStore();
        var map = withProceduralTopic ? OpenSlidesFixtures.FullMap : OpenSlidesFixtures.FullMap.Replace("\"meeting/3/agenda_item_ids\": [20, 21]", "\"meeting/3/agenda_item_ids\": [20]");
        using var document = JsonDocument.Parse(map);
        store.Apply(document.RootElement);
        return OpenSlidesMeetingView.From(store, OpenSlidesFixtures.MeetingId);
    }

    private static PollPlanContext Context(int agendaItemId = 20, bool withProceduralTopic = true, int? delegates = 5, params CompetingMotion[] competitors) {
        var view = View(withProceduralTopic);
        var item = view.AgendaItems.FirstOrDefault(a => a.Id == agendaItemId);
        return new PollPlanContext(3, delegates, item, view, competitors, "SÄA001 Änderung der Geschäftsordnung");
    }

    private static (PollPlan Plan, OpenSlidesPollCreate Poll) Build(PollTemplate template, PollPlanContext context, string? text = null, int? threshold = null) {
        var (plan, error) = AssemblyPollTemplates.Plan(new PollTemplateRequest(template, text, threshold), context);
        if (plan == null)
            throw new InvalidOperationException(error);

        return (plan, plan.Build(plan.ContentObjectId ?? "topic/99"));
    }

    [Test]
    public async Task Continue_debate_is_yes_no_on_existing_procedural_topic() {
        var (plan, poll) = Build(PollTemplate.ContinueDebate, Context());

        await Assert.That(plan.ContentObjectId).IsEqualTo("topic/30");
        await Assert.That(plan.Title).IsEqualTo("SÄA001: Debatte fortsetzen?");
        await Assert.That(plan.Kind).IsEqualTo(PollEvaluationKind.Simple);
        await Assert.That(poll.PollMethod).IsEqualTo("Y");
        await Assert.That(poll.Options.Select(o => o.Text)).IsEquivalentTo(["Ja", "Nein"]);
        await Assert.That(poll.MaxVotes).IsEqualTo(1);
        await Assert.That(poll.GlobalAbstain).IsFalse();
        await Assert.That(poll.EntitledGroupId).IsEqualTo(5);
    }

    [Test]
    public async Task Missing_procedural_topic_is_planned_as_new_topic() {
        var (plan, _) = Build(PollTemplate.ContinueDebate, Context(withProceduralTopic: false));

        await Assert.That(plan.ContentObjectId).IsNull();
        await Assert.That(plan.NewTopicTitle).IsEqualTo("SÄA001: Verfahren");
        await Assert.That(plan.NewTopicParentId).IsEqualTo(20);
    }

    [Test]
    public async Task Topic_item_carries_its_own_polls() {
        var (plan, _) = Build(PollTemplate.Opinion, Context(agendaItemId: 21), "Mehr Pausen?");

        await Assert.That(plan.ContentObjectId).IsEqualTo("topic/30");
        await Assert.That(plan.Kind).IsEqualTo(PollEvaluationKind.NumbersOnly);
        await Assert.That(plan.Title).IsEqualTo("SÄA001: Verfahren: Meinungsbild: Mehr Pausen?");
    }

    [Test]
    public async Task Go_motion_and_support_need_text_and_carry_it() {
        var (go, _) = Build(PollTemplate.GoMotion, Context(), "Schluss der Redeliste");
        var (support, _) = Build(PollTemplate.Support, Context(), "Erika Muster", 20);
        var (_, missing) = AssemblyPollTemplates.Plan(new PollTemplateRequest(PollTemplate.GoMotion, " ", null), Context());

        await Assert.That(go.Title).IsEqualTo("SÄA001: GO-Antrag: Schluss der Redeliste");
        await Assert.That(support.Kind).IsEqualTo(PollEvaluationKind.Threshold);
        await Assert.That(support.Threshold).IsEqualTo(20);
        await Assert.That(missing).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.TextRequired);
    }

    [Test]
    public async Task Threshold_below_one_is_rejected() {
        var (plan, error) = AssemblyPollTemplates.Plan(new PollTemplateRequest(PollTemplate.Support, "Erika Muster", 0), Context());

        await Assert.That(plan).IsNull();
        await Assert.That(error).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.InvalidThreshold);
    }

    [Test]
    public async Task Controversy_threshold_is_ten() {
        var (plan, _) = Build(PollTemplate.Controversy, Context());

        await Assert.That(plan.Kind).IsEqualTo(PollEvaluationKind.Threshold);
        await Assert.That(plan.Threshold).IsEqualTo(10);
    }

    [Test]
    public async Task Final_vote_is_motion_poll_with_abstention() {
        var (plan, poll) = Build(PollTemplate.FinalVote, Context());

        await Assert.That(plan.ContentObjectId).IsEqualTo("motion/12");
        await Assert.That(plan.Kind).IsNull();
        await Assert.That(poll.PollMethod).IsEqualTo("YNA");
        await Assert.That(poll.Options.Single().ContentObjectId).IsEqualTo("motion/12");
        await Assert.That(poll.OnehundredPercentBase).IsEqualTo("YN");
    }

    [Test]
    public async Task Final_vote_requires_motion_item() {
        var (_, error) = AssemblyPollTemplates.Plan(new PollTemplateRequest(PollTemplate.FinalVote, null, null), Context(agendaItemId: 21));

        await Assert.That(error).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.NotAMotion);
    }

    [Test]
    public async Task Acceptance_options_from_competition() {
        var context = Context(competitors: new CompetingMotion(13, "SÄA002 Gegenentwurf"));

        var (plan, poll) = Build(PollTemplate.Competition, context);
        var (runoffPlan, runoff) = Build(PollTemplate.Runoff, context);

        await Assert.That(plan.Kind).IsEqualTo(PollEvaluationKind.Acceptance);
        await Assert.That(poll.Options.Select(o => o.Text)).IsEquivalentTo(["SÄA001 Änderung der Geschäftsordnung", "SÄA002 Gegenentwurf"]);
        await Assert.That(poll.MaxVotes).IsEqualTo(2);
        await Assert.That(poll.MinVotes).IsEqualTo(0);
        await Assert.That(poll.MaxVotesPerOption).IsEqualTo(1);
        await Assert.That(poll.GlobalNo).IsTrue();
        await Assert.That(runoff.MaxVotes).IsEqualTo(1);
        await Assert.That(runoffPlan.Kind).IsEqualTo(PollEvaluationKind.NumbersOnly);
    }

    [Test]
    public async Task Competition_without_competitors_is_refused() {
        var (_, error) = AssemblyPollTemplates.Plan(new PollTemplateRequest(PollTemplate.Competition, null, null), Context());

        await Assert.That(error).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.NoCompetitors);
    }

    [Test]
    public async Task Missing_item_or_group_is_refused() {
        var (_, noItem) = AssemblyPollTemplates.Plan(new PollTemplateRequest(PollTemplate.Block, null, null), Context(agendaItemId: 999));
        var (_, noGroup) = AssemblyPollTemplates.Plan(new PollTemplateRequest(PollTemplate.Block, null, null), Context(delegates: null));

        await Assert.That(noItem).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.NoAgendaItem);
        await Assert.That(noGroup).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.DelegatesGroupMissing);
    }

    [Test]
    public async Task Procedural_topic_found_again_and_missing_listed() {
        var withTopic = View();
        var withoutTopic = View(withProceduralTopic: false);

        await Assert.That(ProceduralTopics.Find(withTopic, withTopic.AgendaItems.Single(a => a.Id == 20))).IsEqualTo("topic/30");
        await Assert.That(ProceduralTopics.MotionsWithout(withTopic)).IsEmpty();
        await Assert.That(ProceduralTopics.MotionsWithout(withoutTopic).Single().Id).IsEqualTo(20);
    }
}
