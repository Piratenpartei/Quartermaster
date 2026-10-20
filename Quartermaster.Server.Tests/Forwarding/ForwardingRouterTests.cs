using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Server.Forwarding;

namespace Quartermaster.Server.Tests.Forwarding;

public class ForwardingRouterTests {
    private static readonly Guid Federal = Guid.NewGuid();
    private static readonly Guid State = Guid.NewGuid();
    private static readonly Guid District = Guid.NewGuid();

    private static ForwardingChapterContext Context(ForwardingMode? stateReduction = null, ForwardingMode? federalReduction = null,
        ForwardingMode? federalApplication = null, Guid? handling = null, ForwardingMode? handlingMode = null, bool withState = true)
        => new(Federal, withState ? State : null, federalApplication, federalReduction, stateReduction, handling, handlingMode);

    private static ForwardingPlan Due(ForwardingChapterContext ctx, decimal amount, bool reduced = true, bool enabled = true, bool partOfApplication = false)
        => ForwardingRouter.PlanDueSelection(ctx, amount, reduced, enabled, 72, 12, partOfApplication);

    [Test]
    public async Task Regular_due_creates_only_federal_regular_ticket() {
        var plan = Due(Context(), 120, reduced: false);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Regular) });
        await Assert.That(plan.StoreRecord).IsFalse();
        await Assert.That(plan.MotionChapterId).IsNull();
    }

    [Test]
    public async Task Regular_due_in_application_creates_nothing() {
        var plan = Due(Context(), 120, reduced: false, partOfApplication: true);

        await Assert.That(plan.Tickets).IsEmpty();
        await Assert.That(plan.StoreRecord).IsFalse();
    }

    [Test]
    public async Task Reduced_amount_at_or_above_minimum_needs_no_decision() {
        var plan = Due(Context(stateReduction: ForwardingMode.Quartermaster), 72);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Regular) });
        await Assert.That(plan.MotionChapterId).IsNull();
    }

    [Test]
    public async Task Reduction_at_state_with_quartermaster_creates_motion_and_note() {
        var plan = Due(Context(stateReduction: ForwardingMode.Quartermaster), 36);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Reduction) });
        await Assert.That(plan.MotionChapterId).IsEqualTo(State);
        await Assert.That(plan.DecidingChapterId).IsEqualTo(State);
        await Assert.That(plan.StoreRecord).IsTrue();
        await Assert.That(plan.RecordStatus).IsEqualTo(DueSelectionStatus.Pending);
        await Assert.That(plan.NoteAfterDecision).IsTrue();
    }

    [Test]
    public async Task Reduction_at_state_with_redmine_creates_two_tickets_federal_first() {
        var plan = Due(Context(stateReduction: ForwardingMode.Redmine), 36);

        await Assert.That(plan.Tickets.ToArray()).IsEquivalentTo(new[] {
            new PlannedTicket(Federal, ForwardingPurpose.Reduction),
            new PlannedTicket(State, ForwardingPurpose.Reduction)
        });
        await Assert.That(plan.Tickets[0].TargetChapterId).IsEqualTo(Federal);
        await Assert.That(plan.MotionChapterId).IsNull();
        await Assert.That(plan.NoteAfterDecision).IsFalse();
    }

    [Test]
    public async Task Reduction_at_state_unset_creates_federal_only() {
        var plan = Due(Context(), 36);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Reduction) });
        await Assert.That(plan.MotionChapterId).IsNull();
        await Assert.That(plan.StoreRecord).IsFalse();
    }

    [Test]
    public async Task Reduction_below_state_minimum_goes_to_federal() {
        var plan = Due(Context(stateReduction: ForwardingMode.Quartermaster, federalReduction: ForwardingMode.Quartermaster), 6);

        await Assert.That(plan.DecidingChapterId).IsEqualTo(Federal);
        await Assert.That(plan.MotionChapterId).IsEqualTo(Federal);
        await Assert.That(plan.NoteAfterDecision).IsTrue();
    }

    [Test]
    public async Task Reduction_without_state_goes_to_federal() {
        var plan = Due(Context(federalReduction: ForwardingMode.Quartermaster, withState: false), 36);

        await Assert.That(plan.DecidingChapterId).IsEqualTo(Federal);
        await Assert.That(plan.MotionChapterId).IsEqualTo(Federal);
    }

    [Test]
    public async Task Federal_reduction_redmine_has_no_note() {
        var plan = Due(Context(federalReduction: ForwardingMode.Redmine), 6);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Reduction) });
        await Assert.That(plan.MotionChapterId).IsNull();
        await Assert.That(plan.NoteAfterDecision).IsFalse();
    }

    [Test]
    public async Task Underage_due_needs_no_decision() {
        var plan = Due(Context(stateReduction: ForwardingMode.Quartermaster), 12, reduced: false);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Regular) });
        await Assert.That(plan.MotionChapterId).IsNull();
    }

    [Test]
    public async Task Switches_off_store_records_without_tickets() {
        var regular = Due(Context(), 120, reduced: false, enabled: false);
        var reduction = Due(Context(), 36, enabled: false);

        await Assert.That(regular.Tickets).IsEmpty();
        await Assert.That(regular.StoreRecord).IsTrue();
        await Assert.That(regular.RecordStatus).IsEqualTo(DueSelectionStatus.AutoApproved);
        await Assert.That(regular.RecordChapterId).IsEqualTo(State);
        await Assert.That(reduction.Tickets).IsEmpty();
        await Assert.That(reduction.MotionChapterId).IsEqualTo(State);
        await Assert.That(reduction.RecordStatus).IsEqualTo(DueSelectionStatus.Pending);
        await Assert.That(reduction.NoteAfterDecision).IsFalse();
    }

    [Test]
    public async Task Application_handling_quartermaster() {
        var plan = ForwardingRouter.PlanApplication(Context(handling: District, handlingMode: ForwardingMode.Quartermaster), District, true);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Application) });
        await Assert.That(plan.MotionChapterId).IsEqualTo(District);
        await Assert.That(plan.StoreRecord).IsTrue();
        await Assert.That(plan.NoteAfterDecision).IsTrue();
    }

    [Test]
    public async Task Application_handling_redmine() {
        var plan = ForwardingRouter.PlanApplication(Context(handling: State, handlingMode: ForwardingMode.Redmine), District, true);

        await Assert.That(plan.Tickets.ToArray()).IsEquivalentTo(new[] {
            new PlannedTicket(Federal, ForwardingPurpose.Application),
            new PlannedTicket(State, ForwardingPurpose.Application)
        });
        await Assert.That(plan.StoreRecord).IsFalse();
        await Assert.That(plan.MotionChapterId).IsNull();
    }

    [Test]
    public async Task Application_abroad_uses_federal_mode() {
        var quartermaster = ForwardingRouter.PlanApplication(Context(federalApplication: ForwardingMode.Quartermaster, withState: false), Federal, true);
        var redmine = ForwardingRouter.PlanApplication(Context(federalApplication: ForwardingMode.Redmine, withState: false), Federal, true);

        await Assert.That(quartermaster.MotionChapterId).IsEqualTo(Federal);
        await Assert.That(quartermaster.NoteAfterDecision).IsTrue();
        await Assert.That(redmine.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Application) });
        await Assert.That(redmine.StoreRecord).IsFalse();
    }

    [Test]
    public async Task Application_lower_chapter_unset_federal_only() {
        var plan = ForwardingRouter.PlanApplication(Context(federalApplication: ForwardingMode.Quartermaster), District, true);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Application) });
        await Assert.That(plan.MotionChapterId).IsNull();
        await Assert.That(plan.StoreRecord).IsFalse();
    }

    [Test]
    public async Task Application_switch_off_is_handled_in_its_chapter() {
        var plan = ForwardingRouter.PlanApplication(Context(), District, false);

        await Assert.That(plan.Tickets).IsEmpty();
        await Assert.That(plan.MotionChapterId).IsEqualTo(District);
        await Assert.That(plan.StoreRecord).IsTrue();
        await Assert.That(plan.NoteAfterDecision).IsFalse();
    }

    [Test]
    public async Task Effective_amount_uses_reduced_amount() {
        var reduced = new DueSelectionDTO { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24, SelectedDue = 0 };
        var regular = new DueSelectionDTO { SelectedValuation = SelectedValuation.MonthlyPayGroup, SelectedDue = 120 };

        await Assert.That(DueAmount.Effective(reduced)).IsEqualTo(24m);
        await Assert.That(DueAmount.NeedsDecision(reduced, 72)).IsTrue();
        await Assert.That(DueAmount.Effective(regular)).IsEqualTo(120m);
        await Assert.That(DueAmount.NeedsDecision(regular, 72)).IsFalse();
    }
    [Test]
    public async Task Application_without_chapter_waits_for_division_linking() {
        var plan = ForwardingRouter.PlanApplication(Context(federalApplication: ForwardingMode.Quartermaster), null, true);

        await Assert.That(plan.Tickets).IsEquivalentTo(new[] { new PlannedTicket(Federal, ForwardingPurpose.Application) });
        await Assert.That(plan.StoreRecord).IsTrue();
        await Assert.That(plan.RecordChapterId).IsNull();
        await Assert.That(plan.MotionChapterId).IsNull();
        await Assert.That(plan.NoteAfterDecision).IsFalse();
    }
}
