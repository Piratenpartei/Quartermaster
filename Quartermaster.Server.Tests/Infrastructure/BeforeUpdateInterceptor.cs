using System;
using System.Data.Common;
using System.Threading;
using LinqToDB.Interceptors;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Runs an action once, right before the first UPDATE of <c>tableName</c> goes to the database. The action writes through its own connection, so
/// it stands in for a concurrent request that commits between a read and the write that depends on it.
/// </summary>
public class BeforeUpdateInterceptor : CommandInterceptor {
    private readonly string _tableName;
    private Action? _action;

    public BeforeUpdateInterceptor(string tableName, Action action) {
        _tableName = tableName;
        _action = action;
    }

    public override DbCommand CommandInitialized(CommandEventData eventData, DbCommand command) {
        if (!IsUpdateOfTable(command.CommandText))
            return command;

        var action = Interlocked.Exchange(ref _action, null);
        action?.Invoke();
        return command;
    }

    private bool IsUpdateOfTable(string commandText) {
        return commandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            && commandText.Contains($"`{_tableName}`", StringComparison.Ordinal);
    }
}
