using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

public class CapturingLogger<T> : ILogger<T> {
    private readonly Lock _lock = new();
    private readonly List<string> _entries = [];

    public List<string> Entries {
        get {
            using (_lock.EnterScope()) {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        using (_lock.EnterScope()) {
            _entries.Add($"{logLevel}: {formatter(state, exception)}{(exception == null ? "" : " " + exception)}");
        }
    }
}
