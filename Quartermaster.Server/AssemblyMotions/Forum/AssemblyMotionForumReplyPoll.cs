using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Options;
using Quartermaster.Data.Users;
using Quartermaster.Server.AssemblyMotions.Notifications;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>
/// Notifies motion authors about new replies in their mirrored forum topics. One listing per category shows which topics grew, only those are read post by post.
/// The first look at a topic only records how far it goes, so replies written before never notify.
/// </summary>
public class AssemblyMotionForumReplyPoll {
    public const int MaxPagesPerCategory = 5;
    public const int BaselineBatchSize = 20;
    public const int MaxChunksPerTopic = 10;

    private static readonly TimeSpan NotAdminWarningInterval = TimeSpan.FromHours(1);
    private static readonly Lock WarningLock = new();
    private static DateTime lastNotAdminWarning = DateTime.MinValue;

    private readonly AssemblyMotionRepository _motions;
    private readonly UserRepository _users;
    private readonly IDiscourseClient _forum;
    private readonly AssemblyMotionNotifier _notifier;
    private readonly OptionRepository _options;
    private readonly ILogger<AssemblyMotionForumReplyPoll> _logger;

    public AssemblyMotionForumReplyPoll(AssemblyMotionRepository motions, UserRepository users, IDiscourseClient forum, AssemblyMotionNotifier notifier,
        OptionRepository options, ILogger<AssemblyMotionForumReplyPoll> logger) {
        _motions = motions;
        _users = users;
        _forum = forum;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>Test seam: forgets when the missing-admin warning was last logged.</summary>
    internal static void ResetNotAdminWarning() {
        using (WarningLock.EnterScope()) {
            lastNotAdminWarning = DateTime.MinValue;
        }
    }

    /// <summary>Returns how many replies were notified. A rate limit ends the run; what was read until then stays recorded.</summary>
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

    private async Task PollCategoryAsync(PollRun run, int categoryId, Dictionary<int, ForumWatchedMotion> byTopic, CancellationToken ct) {
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

            // The listing is ordered by activity, so a page without a grown topic means the older ones did not grow either.
            if (grown.Count == 0 || !listing.HasMore)
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
                if (await IsNotifiedReplyAsync(run, watched, post, ct))
                    Notify(run, watched, post);
                seen = post.PostNumber;
            }
            _motions.SetForumSeenPostNumber(watched.MotionId, seen);
        }
    }

    private async Task<bool> IsNotifiedReplyAsync(PollRun run, ForumWatchedMotion watched, DiscourseReply post, CancellationToken ct) {
        if (post.PostNumber <= 1)
            return false;
        if (post.PostType != DiscourseReply.RegularPostType)
            return false;
        if (post.Deleted || post.Hidden)
            return false;
        if (string.Equals(post.Username, run.Bot.Username, StringComparison.OrdinalIgnoreCase))
            return false;

        var authorForumUserId = await AuthorForumUserIdAsync(run, watched.AuthorMemberId, ct);
        return authorForumUserId != post.UserId;
    }

    private void Notify(PollRun run, ForumWatchedMotion watched, DiscourseReply post) {
        var motion = _motions.Get(watched.MotionId);
        if (motion == null)
            return;

        _notifier.ForumReply(motion, $"{run.Settings.BaseUrl}/t/{watched.TopicId}/{post.PostNumber}");
        run.Notified++;
    }

    /// <summary>Null when the author's forum account cannot be told, so their own replies notify them too.</summary>
    private async Task<int?> AuthorForumUserIdAsync(PollRun run, Guid memberId, CancellationToken ct) {
        if (run.AuthorForumUserIds.TryGetValue(memberId, out var cached))
            return cached;

        var forumUserId = await LookUpAuthorAsync(run, memberId, ct);
        run.AuthorForumUserIds[memberId] = forumUserId;
        return forumUserId;
    }

    private async Task<int?> LookUpAuthorAsync(PollRun run, Guid memberId, CancellationToken ct) {
        if (!run.Bot.IsAdmin)
            return null;

        var keycloakUserId = _users.GetKeycloakUserIdOfMember(memberId);
        if (keycloakUserId == null)
            return null;

        try {
            return await _forum.FindUserIdByKeycloakIdAsync(run.Settings, keycloakUserId, ct);
        } catch (DiscourseException ex) when (ex.Code != DiscourseException.RateLimited) {
            _logger.LogWarning("Forum reply poll could not look up the forum account of member {MemberId}: {Code}", memberId, ex.Code);
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

        _logger.LogWarning("The forum bot account is no admin, so replies by a motion's author notify the author too");
    }

    private class PollRun {
        public PollRun(DiscourseSettings settings, DiscourseCurrentUser bot) {
            Settings = settings;
            Bot = bot;
        }

        public DiscourseSettings Settings { get; }
        public DiscourseCurrentUser Bot { get; }
        public Dictionary<Guid, int?> AuthorForumUserIds { get; } = [];
        public int Notified { get; set; }
    }
}
