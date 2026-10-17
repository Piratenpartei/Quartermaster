using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

/// <summary>In-memory wiki. State is static so the instance the service uses and the test see the same pages; test classes using it run <c>[NotInParallel("FakeWikiBotClient")]</c> and call <see cref="Reset"/> in setup.</summary>
public class FakeWikiBotClient : IWikiBotClient {
    private static readonly Lock StateLock = new();

    public static Dictionary<string, string> Pages { get; } = [];
    public static List<string> Protected { get; } = [];
    public static List<(string Title, string Summary)> Edits { get; } = [];
    public static bool FailNextEdit { get; set; }
    public static bool FailProtect { get; set; }
    public static bool Configured { get; set; } = true;
    public static int LoginCount { get; set; }
    public static int GetCount { get; set; }

    /// <summary>Transforms the stored text before <see cref="GetContentAsync"/> returns it, e.g. to add CRLF and a trailing newline like a wiki might.</summary>
    public static Func<string, string>? TransformReturnedText { get; set; }

    public static Func<string, Task>? OnEdit { get; set; }
    public static Func<string, Task>? OnGetContent { get; set; }

    /// <summary>Exceptions thrown by method name (Login, GetContent, Edit, Protect) on every call.</summary>
    public static Dictionary<string, Exception> ThrowOn { get; } = [];

    public static void Reset() {
        using (StateLock.EnterScope()) {
            Pages.Clear();
            Protected.Clear();
            Edits.Clear();
            ThrowOn.Clear();
            FailNextEdit = false;
            FailProtect = false;
            Configured = true;
            LoginCount = 0;
            GetCount = 0;
            TransformReturnedText = null;
            OnEdit = null;
            OnGetContent = null;
        }
    }

    public Task LoginAsync(WikiBotSettings settings, CancellationToken ct) {
        using (StateLock.EnterScope()) {
            LoginCount++;
        }

        ThrowIfFaulted("Login");
        return Task.CompletedTask;
    }

    public async Task<string?> GetContentAsync(WikiBotSettings settings, string title, CancellationToken ct) {
        using (StateLock.EnterScope()) {
            GetCount++;
        }

        ThrowIfFaulted("GetContent");

        var callback = OnGetContent;
        if (callback != null)
            await callback(title);

        using (StateLock.EnterScope()) {
            if (!Pages.TryGetValue(title, out var text))
                return null;

            return TransformReturnedText == null ? text : TransformReturnedText(text);
        }
    }

    public async Task EditAsync(WikiBotSettings settings, string title, string text, string summary, CancellationToken ct) {
        ThrowIfFaulted("Edit");
        using (StateLock.EnterScope()) {
            if (FailNextEdit) {
                FailNextEdit = false;
                throw new WikiBotException("Fake edit failure", "http_error");
            }
        }

        var callback = OnEdit;
        if (callback != null)
            await callback(title);

        using (StateLock.EnterScope()) {
            Pages[title] = text;
            Edits.Add((title, summary));
        }
    }

    public Task ProtectAsync(WikiBotSettings settings, string title, CancellationToken ct) {
        ThrowIfFaulted("Protect");
        using (StateLock.EnterScope()) {
            if (FailProtect)
                throw new WikiBotException("Fake protect failure", "permissiondenied");

            Protected.Add(title);
        }

        return Task.CompletedTask;
    }

    private static void ThrowIfFaulted(string method) {
        Exception? exception;
        using (StateLock.EnterScope()) {
            if (!Configured)
                throw new WikiBotException("Fake wiki is not configured", "login_failed");

            ThrowOn.TryGetValue(method, out exception);
        }

        if (exception != null)
            throw exception;
    }
}
