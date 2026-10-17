using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyAssistant.Templates;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>
/// The OpenSlides side of a session: the live meeting view, poll settings, automation and evaluation. Pure; owned by
/// <see cref="Session.AssemblySessionState"/> and called under its lock.
/// </summary>
public class AssemblyPollState {
    public const int RecentResults = 3;
    public static readonly TimeSpan ResultVisibleFor = TimeSpan.FromSeconds(15);

    private readonly IReadOnlyDictionary<int, RequiredMajority> _motionMajorities;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<CompetingMotion>> _competitors;
    private readonly HashSet<int> _createdHere = [];
    private readonly PollAutomation _automation = new();
    private readonly Dictionary<int, (PollEvaluationKind Kind, int Threshold)> _registeredKinds = new();
    private readonly Dictionary<int, DateTime> _publishedAt = new();
    private readonly Dictionary<int, string> _lastState = new();

    public bool Enabled { get; }
    public bool Connected { get; private set; }
    public string? Error { get; private set; }
    public OpenSlidesMeetingView View { get; private set; } = OpenSlidesMeetingView.Empty;
    public PollSettings Settings { get; set; }
    public int? DelegatesGroupId { get; }
    public IReadOnlyList<int> ProjectorIds { get; }
    public string StateName => !Enabled ? "Disabled" : Connected ? "Connected" : Error == null ? "Connecting" : "Disconnected";

    public AssemblyPollState(AssemblyPollConfig config) {
        Enabled = config.Enabled;
        Settings = config.Settings;
        DelegatesGroupId = config.DelegatesGroupId;
        ProjectorIds = config.ProjectorIds;
        _motionMajorities = config.Majorities;
        _competitors = config.Competitors;
    }

    public AssemblyPollState(bool enabled, PollSettings settings, IReadOnlyDictionary<int, RequiredMajority> motionMajorities)
        : this(new AssemblyPollConfig(enabled, settings, motionMajorities, null, [], new Dictionary<int, IReadOnlyList<CompetingMotion>>())) { }

    public void SetConnection(bool connected, string? error) {
        Connected = connected;
        Error = connected ? null : error;
    }

    /// <summary>Returns the automation's actions and the ids of polls that were newly published.</summary>
    public (IReadOnlyList<OpenSlidesEffect> Effects, IReadOnlyList<OpenSlidesPollView> Published) OnView(OpenSlidesMeetingView view, DateTime nowUtc) {
        View = view;
        var published = new List<OpenSlidesPollView>();
        foreach (var poll in view.Polls) {
            var previous = _lastState.GetValueOrDefault(poll.Id);
            _lastState[poll.Id] = poll.State;
            if (poll.State == "published" && previous != null && previous != "published") {
                _publishedAt[poll.Id] = nowUtc;
                published.Add(poll);
            }
        }

        return (_automation.OnView(view, Settings, nowUtc, ProjectorIds), published);
    }

    public IReadOnlyList<OpenSlidesEffect> Tick(DateTime nowUtc) {
        return Enabled && Connected ? _automation.Tick(View, Settings, nowUtc) : [];
    }

    public IReadOnlyList<OpenSlidesEffect> Extend(int seconds) {
        return _automation.Extend(View, seconds);
    }

    public OpenSlidesPollView? FindPoll(int pollId) {
        return View.Polls.FirstOrDefault(p => p.Id == pollId);
    }

    public OpenSlidesAgendaItemView? FindAgendaItem(int agendaItemId) {
        return View.AgendaItems.FirstOrDefault(a => a.Id == agendaItemId);
    }

    public void MarkCreatedHere(int pollId) {
        _createdHere.Add(pollId);
    }

    public bool IsCreatedHere(int pollId) {
        return _createdHere.Contains(pollId);
    }

    public IReadOnlyList<CompetingMotion> CompetitorsOf(OpenSlidesAgendaItemView? item) {
        if (item == null || !item.ContentObjectId.StartsWith("motion/"))
            return [];
        if (!int.TryParse(item.ContentObjectId["motion/".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var motionId))
            return [];

        return _competitors.GetValueOrDefault(motionId) ?? [];
    }

    /// <summary>Remembers how a poll created from a template is evaluated (support threshold, acceptance).</summary>
    public void RegisterEvaluation(int pollId, PollEvaluationKind kind, int threshold) {
        _registeredKinds[pollId] = (kind, threshold);
    }

    public PollEvaluationResult? Evaluate(OpenSlidesPollView poll) {
        if (poll.State != "finished" && poll.State != "published")
            return null;

        var (kind, threshold) = EvaluationOf(poll);
        return PollEvaluation.Evaluate(poll, kind, threshold);
    }

    public static string AgendaLabel(OpenSlidesAgendaItemView item) {
        var title = item.MotionNumber != null ? $"{item.MotionNumber}: {item.Title}" : item.Title;
        return item.ItemNumber != null ? $"{item.ItemNumber} {title}" : title;
    }

    public List<AssemblyPollDTO> PollDtos() {
        var running = View.Polls.Where(p => p.State == "started" || p.State == "created");
        var results = View.Polls
            .Where(p => p.State == "finished" || p.State == "published")
            .OrderByDescending(p => p.Id)
            .Take(RecentResults);

        return running.Concat(results).Select(ToDto).ToList();
    }

    public List<AssemblyAgendaItemDTO> AgendaDtos() {
        return View.AgendaItems
            .OrderBy(a => a.Weight)
            .Select(a => new AssemblyAgendaItemDTO { Id = a.Id, Label = AgendaLabel(a), IsMotion = a.ContentObjectId.StartsWith("motion/") })
            .ToList();
    }

    public AssemblyOverlayPollDTO? OverlayPoll(DateTime nowUtc) {
        var running = View.Polls.FirstOrDefault(p => p.State == "started");
        if (running != null) {
            return new AssemblyOverlayPollDTO {
                Title = running.Title,
                Running = true,
                EndsAt = CountdownEnd(),
                LiveVoteCount = running.LiveVoteCount
            };
        }

        var latest = _publishedAt
            .Where(p => nowUtc - p.Value <= ResultVisibleFor)
            .OrderByDescending(p => p.Value)
            .Select(p => FindPoll(p.Key))
            .FirstOrDefault(p => p != null);
        if (latest == null)
            return null;

        var (yes, no, abstain) = PollEvaluation.Counts(latest);
        var evaluation = Evaluate(latest);
        return new AssemblyOverlayPollDTO {
            Title = latest.Title,
            Running = false,
            Yes = yes,
            No = no,
            Abstain = abstain,
            ResultKey = evaluation?.SummaryKey,
            Passed = evaluation?.Passed,
            VisibleUntil = _publishedAt[latest.Id] + ResultVisibleFor
        };
    }

    private AssemblyPollDTO ToDto(OpenSlidesPollView poll) {
        var (yes, no, abstain) = PollEvaluation.Counts(poll);
        var evaluation = Evaluate(poll);
        return new AssemblyPollDTO {
            Id = poll.Id,
            Title = poll.Title,
            State = poll.State,
            LiveVoteCount = poll.LiveVoteCount,
            VotesValid = poll.VotesValid,
            Yes = yes,
            No = no,
            Abstain = abstain,
            EndsAt = poll.State == "started" ? CountdownEnd() : null,
            ResultKey = evaluation?.SummaryKey,
            Passed = evaluation?.Passed,
            CreatedHere = _createdHere.Contains(poll.Id)
        };
    }

    private DateTime? CountdownEnd() {
        var countdown = View.PollCountdown;
        return countdown != null && countdown.Running ? PollAutomation.FromUnix(countdown.CountdownTime) : null;
    }

    // Template polls know their evaluation; a final vote on a motion takes the majority of the motion's type; anything else shows numbers.
    private (PollEvaluationKind Kind, int Threshold) EvaluationOf(OpenSlidesPollView poll) {
        if (_registeredKinds.TryGetValue(poll.Id, out var registered))
            return registered;
        if (!poll.ContentObjectId.StartsWith("motion/"))
            return (PollEvaluationKind.NumbersOnly, 0);
        if (!int.TryParse(poll.ContentObjectId["motion/".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var motionId))
            return (PollEvaluationKind.NumbersOnly, 0);

        var majority = _motionMajorities.GetValueOrDefault(motionId, RequiredMajority.Simple);
        return (majority == RequiredMajority.TwoThirds ? PollEvaluationKind.TwoThirds : PollEvaluationKind.Simple, 0);
    }
}
