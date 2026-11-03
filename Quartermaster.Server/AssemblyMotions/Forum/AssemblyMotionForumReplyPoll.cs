using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Options;
using Quartermaster.Data.Users;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Events;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>
/// Notifies motion authors and subscribers about new replies in mirrored forum topics. One listing per category shows which topics grew, only those are read post by
/// post. The first look at a topic only records how far it goes, so replies written before never notify. Nobody is notified about their own reply when the bot can
/// tell who wrote it.
/// </summary>
public class AssemblyMotionForumReplyPoll {
    public const int MaxPagesPerCategory = 5;
    public const int BaselineBatchSize = 20;
    public const int MaxChunksPerTopic = 10;
    public const int MaxSubscriberLookupsPerRun = 20;

    private static readonly TimeSpan NotAdminWarningInterval = TimeSpan.FromHours(1);
    private static readonly Lock WarningLock = new();
    private static DateTime lastNotAdminWarning = DateTime.MinValue;

    private readonly AssemblyMotionRepository _motions;
    private readonly UserRepository _users;
    private readonly IDiscourseClient _forum;
    private readonly AssemblyMotionNotifier _notifier;
    private readonly OptionRepository _options;
    private readonly AssemblyMotionSubscriptionRepository _subscriptions;
    private readonly EventRepository _events;
    private readonly EventUserVisibility _visibility;
    private readonly ILogger<AssemblyMotionForumReplyPoll> _logger;

    public AssemblyMotionForumReplyPoll(AssemblyMotionRepository motions, UserRepository users, IDiscourseClient forum, AssemblyMotionNotifier notifier,
        OptionRepository options, AssemblyMotionSubscriptionRepository subscriptions, EventRepository events, EventUserVisibility visibility,
        ILogger<AssemblyMotionForumReplyPoll> logger) {
        _motions = motions;
        _users = users;
        _forum = forum;
        _notifier = notifier;
        _options = options;
        _subscriptions = subscriptions;
        _events = events;
        _visibility = visibility;
        _logger = logger;
    }

    /// <summary>Test seam: forgets when the missing-admin warning was last logged.</summary>
    internal static void ResetNotAdminWarning() {
        using (WarningLock.EnterScope()) {
            lastNotAdminWarning = DateTime.MinValue;
        }
    }

    /// <summary>Returns how many replies notified anyone. A rate limit ends the run; what was read until then stays recorded.</summary>
    public async Task<int> PollAsync(DateTime nowUtc, CancellationToken ct) {
        var settings = DiscourseSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return 0;

        var watched = _motions.ListForumWatched();
        if (watched.Count == 0)
            return 0;

        DiscourseCurrentUser bot;
        try {
            bot = await _forum.GetCurrentUserAsync(settings, ct);
        } catch (Exception ex) when (!IsShutdown(ex, ct)) {
            _logger.LogWarning(ex, "Forum reply poll skipped: the session check against the forum failed");
            return 0;
        }

        if (!bot.IsAdmin)
            WarnNotAdmin(nowUtc);

        var run = new PollRun(settings, bot);
        try {
            await BaselineAsync(settings, watched.Where(w => w.SeenPostNumber == null), ct);
            foreach (var category in watched.Where(w => w.SeenPostNumber != null).GroupBy(w => w.CategoryId)) {
                var byTopic = category.DistinctBy(w => w.TopicId).ToDictionary(w => w.TopicId);
                await PollCategoryAsync(run, category.Key, byTopic, ct);
            }
        } catch (DiscourseException ex) when (ex.Code == DiscourseException.RateLimited) {
            _logger.LogWarning("Forum reply poll stops for this run: the forum rate-limits requests (retry after {RetryAfter})", ex.RetryAfter);
        }

        return run.Notified;
    }

    private async Task BaselineAsync(DiscourseSettings settings, IEnumerable<ForumWatchedMotion> unseen, CancellationToken ct) {
        foreach (var watched in unseen.Take(BaselineBatchSize)) {
            try {
                var topic = await _forum.GetTopicAsync(settings, watched.TopicId, ct);
                _motions.SetForumSeenPostNumber(watched.MotionId, topic.HighestPostNumber);
            } catch (DiscourseException ex) when (ex.Code != DiscourseException.RateLimited) {
                _logger.LogWarning("Forum reply poll could not read topic {TopicId} of motion {MotionId}: {Code}", watched.TopicId, watched.MotionId, ex.Code);
            }
        }
    }

    /// <summary>
    /// Pages through the category until every watched topic was listed. A page without a grown watched topic proves nothing:
    /// topics of other events or without a motion can be more active than a watched one with a new reply.
    /// </summary>
    private async Task PollCategoryAsync(PollRun run, int categoryId, Dictionary<int, ForumWatchedMotion> byTopic, CancellationToken ct) {
        var unlisted = byTopic.Keys.ToHashSet();
        for (var page = 0; page < MaxPagesPerCategory; page++) {
            DiscourseTopicListPage listing;
            try {
                listing = await _forum.ListCategoryTopicsAsync(run.Settings, categoryId, page, ct);
            } catch (DiscourseException ex) when (ex.Code != DiscourseException.RateLimited) {
                _logger.LogWarning("Forum reply poll could not list category {CategoryId}: {Code}", categoryId, ex.Code);
                return;
            }

            var grown = listing.Topics
                .Where(t => byTopic.TryGetValue(t.TopicId, out var watched) && t.HighestPostNumber > watched.SeenPostNumber)
                .ToList();
            foreach (var topic in grown)
                await ReadRepliesAsync(run, byTopic[topic.TopicId], topic.HighestPostNumber, ct);

            unlisted.ExceptWith(listing.Topics.Select(t => t.TopicId));
            if (unlisted.Count == 0 || !listing.HasMore)
                return;
        }
    }

    private async Task ReadRepliesAsync(PollRun run, ForumWatchedMotion watched, int highestPostNumber, CancellationToken ct) {
        var seen = watched.SeenPostNumber!.Value;
        for (var chunk = 0; chunk < MaxChunksPerTopic && seen < highestPostNumber; chunk++) {
            IReadOnlyList<DiscourseReply> posts;
            try {
                posts = await _forum.ListPostsAfterAsync(run.Settings, watched.TopicId, seen, ct);
            } catch (DiscourseException ex) when (ex.Code != DiscourseException.RateLimited) {
                _logger.LogWarning("Forum reply poll could not read the posts of topic {TopicId}: {Code}", watched.TopicId, ex.Code);
                return;
            }

            var fresh = posts.Where(p => p.PostNumber > seen).OrderBy(p => p.PostNumber).ToList();
            if (fresh.Count == 0) {
                // Whatever lies between is invisible to the bot or gone, so nothing in it can notify.
                _motions.SetForumSeenPostNumber(watched.MotionId, highestPostNumber);
                return;
            }

            foreach (var post in fresh) {
                if (IsCandidate(run, post))
                    await NotifyAsync(run, watched, post, ct);
                seen = post.PostNumber;
            }
            _motions.SetForumSeenPostNumber(watched.MotionId, seen);
        }
    }

    private static bool IsCandidate(PollRun run, DiscourseReply post) {
        if (post.PostNumber <= 1)
            return false;
        if (post.PostType != DiscourseReply.RegularPostType)
            return false;
        if (post.Deleted || post.Hidden)
            return false;
        return !string.Equals(post.Username, run.Bot.Username, StringComparison.OrdinalIgnoreCase);
    }

    private async Task NotifyAsync(PollRun run, ForumWatchedMotion watched, DiscourseReply post, CancellationToken ct) {
        var motion = _motions.Get(watched.MotionId);
        if (motion == null)
            return;

        // Every lookup runs before anything is queued, so a rate limit in between leaves the reply unread and nothing sent twice.
        var authorForumUserId = watched.AuthorUserId == null ? null : await UserForumUserIdAsync(run, watched.AuthorUserId.Value, ct);
        var notifyAuthor = !IsOwnReply(authorForumUserId, post);
        var subscribers = new List<Guid>();
        foreach (var userId in VisibleSubscribers(run, watched)) {
            if (!IsOwnReply(await SubscriberForumUserIdAsync(run, userId, ct), post))
                subscribers.Add(userId);
        }

        var replyUrl = $"{run.Settings.BaseUrl}/t/{watched.TopicId}/{post.PostNumber}";
        if (notifyAuthor)
            _notifier.ForumReply(motion, replyUrl);
        if (subscribers.Count > 0)
            _notifier.SubscribedForumReply(motion, replyUrl, subscribers);
        if (notifyAuthor || subscribers.Count > 0)
            run.Notified++;
    }

    /// <summary>Only a known forum account can be recognised; an unknown one or a removed post author counts as someone else.</summary>
    private static bool IsOwnReply(int? forumUserId, DiscourseReply post) {
        return forumUserId != null && forumUserId == post.UserId;
    }

    /// <summary>The motion's subscribers without its author who can still see the event, loaded once per run.</summary>
    private List<Guid> VisibleSubscribers(PollRun run, ForumWatchedMotion watched) {
        if (run.VisibleSubscribers.TryGetValue(watched.MotionId, out var cached))
            return cached;

        var ev = _events.Get(watched.EventId);
        var visible = ev == null
            ? []
            : _subscriptions.ListUserIdsForMotion(watched.MotionId)
                .Where(userId => userId != watched.AuthorUserId && _visibility.IsVisibleTo(ev, userId))
                .ToList();
        run.VisibleSubscribers[watched.MotionId] = visible;
        return visible;
    }

    /// <summary>
    /// Like <see cref="UserForumUserIdAsync"/>, but only <see cref="MaxSubscriberLookupsPerRun"/> subscribers are looked up per run, so a motion with many
    /// subscribers cannot exhaust the forum's rate limit; the others count as unknown.
    /// </summary>
    private async Task<int?> SubscriberForumUserIdAsync(PollRun run, Guid userId, CancellationToken ct) {
        if (run.UserForumUserIds.TryGetValue(userId, out var cached))
            return cached;
        if (run.SubscriberLookups >= MaxSubscriberLookupsPerRun)
            return null;

        run.SubscriberLookups++;
        return await UserForumUserIdAsync(run, userId, ct);
    }

    /// <summary>Null when the user's forum account cannot be told, so their own replies notify them too.</summary>
    private async Task<int?> UserForumUserIdAsync(PollRun run, Guid userId, CancellationToken ct) {
        if (run.UserForumUserIds.TryGetValue(userId, out var cached))
            return cached;

        var forumUserId = await LookUpUserAsync(run, userId, ct);
        run.UserForumUserIds[userId] = forumUserId;
        return forumUserId;
    }

    private async Task<int?> LookUpUserAsync(PollRun run, Guid userId, CancellationToken ct) {
        if (!run.Bot.IsAdmin)
            return null;

        var keycloakUserId = _users.Get(userId)?.KeycloakUserId;
        if (string.IsNullOrEmpty(keycloakUserId))
            return null;

        try {
            return await _forum.FindUserIdByKeycloakIdAsync(run.Settings, keycloakUserId, ct);
        } catch (DiscourseException ex) when (ex.Code != DiscourseException.RateLimited) {
            _logger.LogWarning("Forum reply poll could not look up the forum account of user {UserId}: {Code}", userId, ex.Code);
            return null;
        }
    }

    private static bool IsShutdown(Exception exception, CancellationToken ct) {
        return exception is OperationCanceledException && ct.IsCancellationRequested;
    }

    private void WarnNotAdmin(DateTime nowUtc) {
        using (WarningLock.EnterScope()) {
            if (nowUtc - lastNotAdminWarning < NotAdminWarningInterval)
                return;
            lastNotAdminWarning = nowUtc;
        }

        _logger.LogWarning("The forum bot account is no admin, so replies by a motion's author or subscribers notify them too");
    }

    private class PollRun {
        public PollRun(DiscourseSettings settings, DiscourseCurrentUser bot) {
            Settings = settings;
            Bot = bot;
        }

        public DiscourseSettings Settings { get; }
        public DiscourseCurrentUser Bot { get; }
        public Dictionary<Guid, int?> UserForumUserIds { get; } = [];
        public Dictionary<Guid, List<Guid>> VisibleSubscribers { get; } = [];
        public int SubscriberLookups { get; set; }
        public int Notified { get; set; }
    }
}
