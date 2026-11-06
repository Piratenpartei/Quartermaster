using System;
using System.Threading.Tasks;
using Quartermaster.Blazor.Components;

namespace Quartermaster.Server.Tests.Components;

public class AsyncButtonStateTests {
    [Test]
    public async Task Starts_ready() {
        await Assert.That(new AsyncButtonState().Phase).IsEqualTo(AsyncButtonPhase.Ready);
    }

    [Test]
    public async Task A_start_makes_it_busy_and_a_second_start_is_refused() {
        var state = new AsyncButtonState();

        var first = state.TryStart();
        var second = state.TryStart();

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(state.Phase).IsEqualTo(AsyncButtonPhase.Busy);
    }

    [Test]
    public async Task A_success_shows_done_and_a_failure_failed() {
        var succeeded = new AsyncButtonState();
        var failed = new AsyncButtonState();
        succeeded.TryStart();
        failed.TryStart();

        succeeded.Finish(true);
        failed.Finish(false);

        await Assert.That(succeeded.Phase).IsEqualTo(AsyncButtonPhase.Done);
        await Assert.That(failed.Phase).IsEqualTo(AsyncButtonPhase.Failed);
    }

    [Test]
    public async Task Reset_returns_to_ready() {
        var state = new AsyncButtonState();
        state.TryStart();
        state.Finish(true);

        state.Reset();

        await Assert.That(state.Phase).IsEqualTo(AsyncButtonPhase.Ready);
        await Assert.That(state.TryStart()).IsTrue();
    }

    /// <summary>A page left while the result still shows disposes the button; the pending reset must not touch it.</summary>
    [Test]
    public async Task After_disposal_finish_and_reset_change_nothing() {
        var state = new AsyncButtonState();
        state.TryStart();

        state.Dispose();
        state.Finish(true);
        state.Reset();

        await Assert.That(state.Phase).IsEqualTo(AsyncButtonPhase.Busy);
        await Assert.That(state.TryStart()).IsFalse();
    }

    [Test]
    public async Task The_delayed_reset_returns_to_ready() {
        var state = new AsyncButtonState();
        state.TryStart();
        state.Finish(true);

        var reset = await state.ResetAfterDelayAsync(TimeSpan.FromMilliseconds(10));

        await Assert.That(reset).IsTrue();
        await Assert.That(state.Phase).IsEqualTo(AsyncButtonPhase.Ready);
    }

    /// <summary>A page left while the action still runs disposes the button before its reset starts; that must not throw.</summary>
    [Test]
    public async Task A_delayed_reset_after_disposal_returns_false_without_throwing() {
        var state = new AsyncButtonState();
        state.TryStart();

        state.Dispose();
        var finished = state.Finish(true);
        var reset = await state.ResetAfterDelayAsync(TimeSpan.FromMilliseconds(10));

        await Assert.That(finished).IsFalse();
        await Assert.That(reset).IsFalse();
    }

    [Test]
    public async Task Disposal_during_the_delayed_reset_cancels_it() {
        var state = new AsyncButtonState();
        state.TryStart();
        state.Finish(true);

        var pending = state.ResetAfterDelayAsync(TimeSpan.FromSeconds(30));
        state.Dispose();
        var reset = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(reset).IsFalse();
        await Assert.That(state.Phase).IsEqualTo(AsyncButtonPhase.Done);
    }
}
