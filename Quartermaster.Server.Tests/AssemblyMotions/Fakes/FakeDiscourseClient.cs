using Quartermaster.Server.AssemblyMotions.Forum;

namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

/// <summary>In-memory forum. State is static so the instance the service uses and the test see the same topics; test classes using it run <c>[NotInParallel("FakeDiscourseClient")]</c> and call <see cref="Reset"/> in setup.</summary>
public class FakeDiscourseClient : IDiscourseClient {
    private static readonly Lock StateLock = new();
    private static int nextId = 100;

    public static Dictionary<int, FakeDiscourseTopic> Topics { get; } = [];
    public static List<string> Calls { get; } = [];
    public static List<(int PostId, string Reason)> Edits { get; } = [];

    /// <summary>Exceptions thrown by method name (CurrentUser, Category, Find, Topic, Post, Create, Edit, Title) on every call.</summary>
    public static Dictionary<string, Exception> ThrowOn { get; } = [];

    /// <summary>Create and edit refuse a raw text longer than this with a 422, like a forum with a lower max_post_length.</summary>
    public static int? RejectRawLongerThan { get; set; }

    public static DiscourseCurrentUser CurrentUser { get; set; } = new("antragsbot", true);
    public static Dictionary<int, DiscourseCategory> Categories { get; } = [];
    public static Func<Task>? OnCreate { get; set; }

    public static void Reset() {
        using (StateLock.EnterScope()) {
            Topics.Clear();
            Calls.Clear();
            Edits.Clear();
            ThrowOn.Clear();
            Categories.Clear();
            RejectRawLongerThan = null;
            CurrentUser = new DiscourseCurrentUser("antragsbot", true);
            OnCreate = null;
        }
    }

    public static int Count(string method) {
        using (StateLock.EnterScope()) {
            return Calls.Count(c => c == method);
        }
    }

    public static FakeDiscourseTopic AddTopic(string title, string raw, string? externalId = null, int categoryId = 7) {
        using (StateLock.EnterScope()) {
            var topic = new FakeDiscourseTopic { TopicId = nextId++, PostId = nextId++, Title = title, Raw = raw, ExternalId = externalId, CategoryId = categoryId };
            Topics[topic.TopicId] = topic;
            return topic;
        }
    }

    public Task<DiscourseCurrentUser> GetCurrentUserAsync(DiscourseSettings settings, CancellationToken ct) {
        Record("CurrentUser");
        return Task.FromResult(CurrentUser);
    }

    public Task<DiscourseCategory> GetCategoryAsync(DiscourseSettings settings, int categoryId, CancellationToken ct) {
        Record("Category");
        using (StateLock.EnterScope()) {
            if (!Categories.TryGetValue(categoryId, out var category))
                throw new DiscourseException("Im Forum nicht gefunden.", DiscourseException.NotFound, 404);
            return Task.FromResult(category);
        }
    }

    public Task<DiscourseTopic?> FindTopicByExternalIdAsync(DiscourseSettings settings, string externalId, CancellationToken ct) {
        Record("Find");
        using (StateLock.EnterScope()) {
            var topic = Topics.Values.FirstOrDefault(t => t.ExternalId == externalId);
            return Task.FromResult(topic?.ToTopic());
        }
    }

    public Task<DiscourseTopic> GetTopicAsync(DiscourseSettings settings, int topicId, CancellationToken ct) {
        Record("Topic");
        return Task.FromResult(Find(topicId).ToTopic());
    }

    public Task<DiscoursePost> GetPostAsync(DiscourseSettings settings, int postId, CancellationToken ct) {
        Record("Post");
        using (StateLock.EnterScope()) {
            var topic = Topics.Values.FirstOrDefault(t => t.PostId == postId)
                ?? throw new DiscourseException("Im Forum nicht gefunden.", DiscourseException.NotFound, 404);
            return Task.FromResult(new DiscoursePost(topic.PostId, topic.Raw, topic.PostDeleted));
        }
    }

    public async Task<DiscourseTopic> CreateTopicAsync(DiscourseSettings settings, string title, string raw, int categoryId, string externalId, CancellationToken ct) {
        Record("Create");
        RejectIfTooLong(raw);
        var callback = OnCreate;
        if (callback != null)
            await callback();

        using (StateLock.EnterScope()) {
            if (Topics.Values.Any(t => t.ExternalId == externalId))
                throw new DiscourseException("External has already been taken", DiscourseException.Rejected, 422);
        }

        return AddTopic(title, raw, externalId, categoryId).ToTopic();
    }

    public Task EditPostAsync(DiscourseSettings settings, int postId, string raw, string editReason, CancellationToken ct) {
        Record("Edit");
        RejectIfTooLong(raw);
        using (StateLock.EnterScope()) {
            var topic = Topics.Values.First(t => t.PostId == postId);
            topic.Raw = raw;
            Edits.Add((postId, editReason));
        }

        return Task.CompletedTask;
    }

    public Task UpdateTopicTitleAsync(DiscourseSettings settings, int topicId, string title, CancellationToken ct) {
        Record("Title");
        Find(topicId).Title = title;
        return Task.CompletedTask;
    }

    private static FakeDiscourseTopic Find(int topicId) {
        using (StateLock.EnterScope()) {
            if (!Topics.TryGetValue(topicId, out var topic))
                throw new DiscourseException("Im Forum nicht gefunden.", DiscourseException.NotFound, 404);
            return topic;
        }
    }

    private static void RejectIfTooLong(string raw) {
        if (RejectRawLongerThan != null && raw.Length > RejectRawLongerThan)
            throw new DiscourseException("Der Beitrag ist zu lang.", DiscourseException.Rejected, 422);
    }

    private static void Record(string method) {
        Exception? exception;
        using (StateLock.EnterScope()) {
            Calls.Add(method);
            ThrowOn.TryGetValue(method, out exception);
        }

        if (exception != null)
            throw exception;
    }
}

public class FakeDiscourseTopic {
    public int TopicId { get; set; }
    public int PostId { get; set; }
    public string Title { get; set; } = "";
    public string Raw { get; set; } = "";
    public string? ExternalId { get; set; }
    public int CategoryId { get; set; }
    public bool Deleted { get; set; }
    public bool PostDeleted { get; set; }

    public DiscourseTopic ToTopic() {
        return new DiscourseTopic(TopicId, PostId, Title, Deleted);
    }
}
