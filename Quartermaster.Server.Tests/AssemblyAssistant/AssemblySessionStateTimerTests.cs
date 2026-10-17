using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class AssemblySessionStateTimerTests {
    private static AssemblySessionFixture SpeakerOnFloor(QueueKind kind = QueueKind.Pro) {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        var channel = kind switch {
            QueueKind.Question => AssemblySessionFixture.Question,
            QueueKind.Point => AssemblySessionFixture.Point,
            QueueKind.Contra => AssemblySessionFixture.Contra,
            _ => AssemblySessionFixture.Pro
        };
        fixture.Move(10, channel);
        fixture.State.GiveFloor(fixture.State.Version, fixture.Entry(kind, "Anna").Key, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Speaker);
        return fixture;
    }

    [Test]
    public async Task Timer_starts_on_first_voice_packet() {
        var fixture = SpeakerOnFloor();
        fixture.Advance(TimeSpan.FromSeconds(5));

        await Assert.That(fixture.FloorOf(10)!.Timer.StartedAt).IsNull();
        var effects = fixture.State.OnSpeaking(10, true, fixture.Now);

        var timer = fixture.FloorOf(10)!.Timer;
        await Assert.That(timer.StartedAt).IsEqualTo(fixture.Now);
        await Assert.That(timer.EndsAt).IsEqualTo(fixture.Now.AddSeconds(180));
        var open = effects.Log.Single();
        await Assert.That(open.Kind).IsEqualTo(AssemblyLogKind.Speech);
        await Assert.That(open.EndedAt).IsNull();
        await Assert.That(open.AllottedSeconds).IsEqualTo(180);
    }

    [Test]
    public async Task Speaking_does_not_bump_version() {
        var fixture = SpeakerOnFloor();
        var version = fixture.State.Version;

        var effects = fixture.State.OnSpeaking(10, true, fixture.Now);

        await Assert.That(effects.Changed).IsTrue();
        await Assert.That(fixture.State.Version).IsEqualTo(version);
        await Assert.That(fixture.State.Snapshot(fixture.Now).SpeakingSessions).Contains(10u);
    }

    [Test]
    public async Task Offer_start_now_after_20_seconds() {
        var fixture = SpeakerOnFloor();

        fixture.Advance(TimeSpan.FromSeconds(19));
        await Assert.That(fixture.FloorOf(10)!.Timer.OfferStartNow).IsFalse();
        fixture.Advance(TimeSpan.FromSeconds(1));
        await Assert.That(fixture.FloorOf(10)!.Timer.OfferStartNow).IsTrue();

        var (result, effects) = fixture.State.StartTimerNow(fixture.State.Version, 10, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(fixture.FloorOf(10)!.Timer.StartedAt).IsEqualTo(fixture.Now);
        await Assert.That(effects.Log.Any(l => l.Kind == AssemblyLogKind.Speech && l.StartedAt == fixture.Now)).IsTrue();
    }

    [Test]
    public async Task Warning_text_once_at_30_seconds() {
        var fixture = SpeakerOnFloor();
        fixture.State.OnSpeaking(10, true, fixture.Now);

        fixture.Advance(TimeSpan.FromSeconds(149));
        await Assert.That(fixture.State.Tick(fixture.Now).Texts).IsEmpty();
        fixture.Advance(TimeSpan.FromSeconds(1));
        var warning = fixture.State.Tick(fixture.Now).Texts.Single();
        fixture.Advance(TimeSpan.FromSeconds(1));
        var again = fixture.State.Tick(fixture.Now).Texts;

        await Assert.That(warning).IsEqualTo(new AssemblyText(10, "Noch 30 Sekunden"));
        await Assert.That(again).IsEmpty();
    }

    [Test]
    public async Task Expired_text_once_at_zero_and_overrun_counts() {
        var fixture = SpeakerOnFloor();
        fixture.State.OnSpeaking(10, true, fixture.Now);
        fixture.Advance(TimeSpan.FromSeconds(150));
        fixture.State.Tick(fixture.Now);

        fixture.Advance(TimeSpan.FromSeconds(30));
        var expired = fixture.State.Tick(fixture.Now).Texts.Single();
        fixture.Advance(TimeSpan.FromSeconds(12));
        var again = fixture.State.Tick(fixture.Now).Texts;

        await Assert.That(expired.Text).IsEqualTo("Die Redezeit ist abgelaufen");
        await Assert.That(again).IsEmpty();
        var timer = fixture.FloorOf(10)!.Timer;
        await Assert.That(timer.EndsAt).IsEqualTo(fixture.Now.AddSeconds(-12));
        await Assert.That(fixture.FloorOf(10)).IsNotNull();
    }

    [Test]
    public async Task Pause_and_resume_shift_end() {
        var fixture = SpeakerOnFloor();
        fixture.State.OnSpeaking(10, true, fixture.Now);
        var originalEnd = fixture.FloorOf(10)!.Timer.EndsAt!.Value;
        fixture.Advance(TimeSpan.FromSeconds(60));

        fixture.State.PauseTimer(fixture.State.Version, 10, AssemblySessionFixture.Actor, fixture.Now);
        var paused = fixture.FloorOf(10)!.Timer;
        fixture.Advance(TimeSpan.FromSeconds(45));
        fixture.State.ResumeTimer(fixture.State.Version, 10, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(paused.Paused).IsTrue();
        await Assert.That(paused.RemainingSeconds).IsEqualTo(120);
        await Assert.That(fixture.FloorOf(10)!.Timer.EndsAt).IsEqualTo(originalEnd.AddSeconds(45));
    }

    [Test]
    public async Task Add_minute_extends_and_rearms_warning() {
        var fixture = SpeakerOnFloor();
        fixture.State.OnSpeaking(10, true, fixture.Now);
        fixture.Advance(TimeSpan.FromSeconds(160));
        await Assert.That(fixture.State.Tick(fixture.Now).Texts.Count).IsEqualTo(1);

        fixture.State.AddMinute(fixture.State.Version, 10, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(fixture.FloorOf(10)!.Timer.AllottedSeconds).IsEqualTo(240);

        fixture.Advance(TimeSpan.FromSeconds(50));
        await Assert.That(fixture.State.Tick(fixture.Now).Texts.Single().Text).IsEqualTo("Noch 30 Sekunden");
    }

    [Test]
    public async Task Set_speech_time_validates_range() {
        var fixture = SpeakerOnFloor();

        var (tooLong, _) = fixture.State.SetSpeechTime(fixture.State.Version, 10, 7200, AssemblySessionFixture.Actor, fixture.Now);
        var (ok, _) = fixture.State.SetSpeechTime(fixture.State.Version, 10, 90, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(tooLong.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.InvalidTime);
        await Assert.That(ok.Accepted).IsTrue();
        await Assert.That(fixture.FloorOf(10)!.Timer.AllottedSeconds).IsEqualTo(90);
    }

    [Test]
    public async Task Motion_override_applies_to_following_speeches_and_resets_on_item_change() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "SÄA001", null, AssemblyItemKind.Motion, AssemblySessionFixture.Actor, fixture.Now);

        fixture.State.SetMotionSpeechMinutes(fixture.State.Version, 2, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.Move(10, AssemblySessionFixture.Speaker);
        await Assert.That(fixture.FloorOf(10)!.Timer.AllottedSeconds).IsEqualTo(120);

        fixture.State.SetItem(fixture.State.Version, "SÄA002", null, AssemblyItemKind.Motion, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(11, AssemblySessionFixture.Contra);
        fixture.Move(11, AssemblySessionFixture.Speaker);

        await Assert.That(fixture.State.MotionSpeechMinutes).IsNull();
        await Assert.That(fixture.FloorOf(11)!.Timer.AllottedSeconds).IsEqualTo(180);
    }

    [Test]
    public async Task Question_queue_uses_question_time() {
        var fixture = SpeakerOnFloor(QueueKind.Question);

        var floor = fixture.FloorOf(10)!;

        await Assert.That(floor.Role).IsEqualTo(SpeakerRole.Question);
        await Assert.That(floor.Timer.AllottedSeconds).IsEqualTo(30);
    }

    [Test]
    public async Task Short_speech_gets_no_warning_only_expiry() {
        var fixture = SpeakerOnFloor(QueueKind.Question);
        fixture.State.OnSpeaking(10, true, fixture.Now);

        fixture.Advance(TimeSpan.FromSeconds(1));
        var early = fixture.State.Tick(fixture.Now).Texts;
        fixture.Advance(TimeSpan.FromSeconds(29));
        var atEnd = fixture.State.Tick(fixture.Now).Texts;

        await Assert.That(early).IsEmpty();
        await Assert.That(atEnd.Single().Text).IsEqualTo("Die Redezeit ist abgelaufen");
    }

    [Test]
    public async Task Point_of_order_is_untimed() {
        var fixture = SpeakerOnFloor(QueueKind.Point);
        fixture.State.OnSpeaking(10, true, fixture.Now);
        fixture.Advance(TimeSpan.FromMinutes(10));

        var (addMinute, _) = fixture.State.AddMinute(fixture.State.Version, 10, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(fixture.FloorOf(10)!.Timer.AllottedSeconds).IsNull();
        await Assert.That(fixture.State.Tick(fixture.Now).Texts).IsEmpty();
        await Assert.That(addMinute.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.Untimed);
    }

    [Test]
    public async Task Recovery_resumes_timer_from_log() {
        var fixture = new AssemblySessionFixture();
        var startedAt = fixture.Now.AddSeconds(-100);
        fixture.User(10, "Anna", channel: AssemblySessionFixture.Speaker);
        fixture.State.PrepareRecovery(new AssemblyLogEntry {
            Kind = AssemblyLogKind.Speech,
            MumbleName = "Anna",
            StartedAt = startedAt,
            AllottedSeconds = 180
        });

        fixture.Sync();

        var timer = fixture.FloorOf(10)!.Timer;
        await Assert.That(timer.StartedAt).IsEqualTo(startedAt);
        await Assert.That(timer.EndsAt).IsEqualTo(startedAt.AddSeconds(180));
    }

    [Test]
    public async Task Overlay_shows_speaker_before_motion_author() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Antragstellerin", channel: AssemblySessionFixture.MotionAuthor);
        fixture.User(11, "Anna", channel: AssemblySessionFixture.Speaker);
        fixture.Sync();

        var overlay = fixture.State.OverlaySnapshot(fixture.Now);

        await Assert.That(overlay.SpeakerName).IsEqualTo("Anna");
        await Assert.That(overlay.SpeakerRole).IsEqualTo(SpeakerRole.Manual);
    }
}
