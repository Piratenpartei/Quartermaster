using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartermaster.Server.Chapters;

namespace Quartermaster.Server.Tests.Chapters;

public class TakeOverScheduleTests {
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TakeOverCandidate State = new(Guid.NewGuid(), "Land", 28, true);
    private static readonly TakeOverCandidate Federal = new(Guid.NewGuid(), "Bund", 62, true);

    [Test]
    public async Task Nothing_due_before_first_deadline() {
        var due = TakeOverSchedule.Due([State, Federal], Start, Start.AddDays(27));

        await Assert.That(due).IsNull();
    }

    [Test]
    public async Task State_takes_over_at_its_deadline() {
        var due = TakeOverSchedule.Due([State, Federal], Start, Start.AddDays(28));

        await Assert.That(due!.ChapterId).IsEqualTo(State.ChapterId);
        await Assert.That(due.DueAt).IsEqualTo(Start.AddDays(28));
    }

    [Test]
    public async Task Federal_takes_over_at_its_deadline() {
        var due = TakeOverSchedule.Due([State, Federal], Start, Start.AddDays(62));

        await Assert.That(due!.ChapterId).IsEqualTo(Federal.ChapterId);
    }

    [Test]
    public async Task Several_passed_goes_to_highest() {
        var due = TakeOverSchedule.Due([State, Federal], Start, Start.AddDays(200));

        await Assert.That(due!.ChapterId).IsEqualTo(Federal.ChapterId);
    }

    [Test]
    public async Task Candidate_without_period_is_passed_over() {
        var district = new TakeOverCandidate(Guid.NewGuid(), "Bezirk", null, true);

        var due = TakeOverSchedule.Due([district, State, Federal], Start, Start.AddDays(30));

        await Assert.That(due!.ChapterId).IsEqualTo(State.ChapterId);
    }

    [Test]
    public async Task Candidate_not_handling_applications_is_passed_over() {
        var redmineState = State with { HandlesApplications = false };

        var due = TakeOverSchedule.Due([redmineState, Federal], Start, Start.AddDays(30));
        var next = TakeOverSchedule.Next([redmineState, Federal], Start, Start.AddDays(30));

        await Assert.That(due).IsNull();
        await Assert.That(next!.ChapterId).IsEqualTo(Federal.ChapterId);
    }

    [Test]
    public async Task Next_is_earliest_future_deadline() {
        var next = TakeOverSchedule.Next([State, Federal], Start, Start.AddDays(10));
        var afterState = TakeOverSchedule.Next([State, Federal], Start, Start.AddDays(28));

        await Assert.That(next!.ChapterId).IsEqualTo(State.ChapterId);
        await Assert.That(afterState!.ChapterId).IsEqualTo(Federal.ChapterId);
        await Assert.That(afterState.DueAt).IsEqualTo(Start.AddDays(62));
    }

    [Test]
    public async Task Next_is_null_when_nothing_ahead() {
        var next = TakeOverSchedule.Next([State, Federal], Start, Start.AddDays(62));

        await Assert.That(next).IsNull();
    }

    [Test]
    public async Task Empty_list_gives_null() {
        List<TakeOverCandidate> none = [];

        await Assert.That(TakeOverSchedule.Due(none, Start, Start.AddDays(500))).IsNull();
        await Assert.That(TakeOverSchedule.Next(none, Start, Start)).IsNull();
    }
}
