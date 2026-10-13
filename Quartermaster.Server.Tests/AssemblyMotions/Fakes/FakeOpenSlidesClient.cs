using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

/// <summary>In-memory OpenSlides. State is static so the instance the service uses and the test see the same data; test classes using it run <c>[NotInParallel("FakeOpenSlidesClient")]</c> and call <see cref="Reset"/> in setup.</summary>
public class FakeOpenSlidesClient : IOpenSlidesClient {
    private static readonly Lock StateLock = new();
    private static readonly Dictionary<string, Exception> CreateFailures = [];
    private static int nextId = 100;

    public static List<FakeOpenSlidesCategory> Categories { get; } = [];
    public static List<FakeOpenSlidesMotion> Motions { get; } = [];
    public static List<OpenSlidesMotionUpdate> Updates { get; } = [];
    public static List<CancellationToken> ReceivedTokens { get; } = [];
    public static int LoginCount { get; set; }
    public static Exception? LoginFailure { get; set; }
    public static Func<string, Task>? OnCreateMotion { get; set; }
    public static string? CategoryFailurePrefix { get; set; }

    public static void Reset() {
        lock (StateLock) {
            Categories.Clear();
            Motions.Clear();
            Updates.Clear();
            ReceivedTokens.Clear();
            CreateFailures.Clear();
            LoginCount = 0;
            LoginFailure = null;
            OnCreateMotion = null;
            CategoryFailurePrefix = null;
            nextId = 100;
        }
    }

    public static void FailCreateFor(string number, Exception? exception = null) {
        lock (StateLock) {
            CreateFailures[number] = exception ?? new OpenSlidesException($"Fake create failure for {number}", "action_failed");
        }
    }

    public static void ClearCreateFailures() {
        lock (StateLock) {
            CreateFailures.Clear();
        }
    }

    private static void RequireUncancellable(CancellationToken ct) {
        ReceivedTokens.Add(ct);
        if (ct.CanBeCanceled)
            throw new InvalidOperationException("The request token must not reach create or update calls.");
    }

    public Task LoginAsync(OpenSlidesSettings settings, CancellationToken ct) {
        lock (StateLock) {
            LoginCount++;
            if (LoginFailure != null)
                throw LoginFailure;
        }

        return Task.CompletedTask;
    }

    public Task<int> CreateCategoryAsync(OpenSlidesSettings settings, int meetingId, string name, string prefix, CancellationToken ct) {
        lock (StateLock) {
            RequireUncancellable(ct);
            if (CategoryFailurePrefix == prefix)
                throw new OpenSlidesException($"Fake category failure for {prefix}", "action_failed");

            var category = new FakeOpenSlidesCategory(nextId++, meetingId, name, prefix);
            Categories.Add(category);
            return Task.FromResult(category.Id);
        }
    }

    public async Task<int> CreateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionCreate motion, CancellationToken ct) {
        lock (StateLock) {
            RequireUncancellable(ct);
        }

        var callback = OnCreateMotion;
        if (callback != null)
            await callback(motion.Number);

        lock (StateLock) {
            if (CreateFailures.TryGetValue(motion.Number, out var failure))
                throw failure;

            var stored = new FakeOpenSlidesMotion {
                Id = nextId++,
                MeetingId = motion.MeetingId,
                Title = motion.Title,
                Number = motion.Number,
                TextHtml = motion.TextHtml,
                ReasonHtml = motion.ReasonHtml,
                CategoryId = motion.CategoryId,
                AdditionalSubmitter = motion.AdditionalSubmitter
            };
            Motions.Add(stored);
            return stored.Id;
        }
    }

    public Task UpdateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionUpdate motion, CancellationToken ct) {
        lock (StateLock) {
            RequireUncancellable(ct);
            var stored = Motions.First(m => m.Id == motion.MotionId);
            stored.Title = motion.Title;
            stored.TextHtml = motion.TextHtml;
            stored.ReasonHtml = motion.ReasonHtml;
            stored.AdditionalSubmitter = motion.AdditionalSubmitter;
            Updates.Add(motion);
        }

        return Task.CompletedTask;
    }
}

public record FakeOpenSlidesCategory(int Id, int MeetingId, string Name, string Prefix);

public class FakeOpenSlidesMotion {
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public string Title { get; set; } = "";
    public string Number { get; set; } = "";
    public string TextHtml { get; set; } = "";
    public string? ReasonHtml { get; set; }
    public int CategoryId { get; set; }
    public string AdditionalSubmitter { get; set; } = "";
}
