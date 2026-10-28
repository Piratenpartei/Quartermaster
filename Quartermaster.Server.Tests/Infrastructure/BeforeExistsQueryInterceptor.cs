using System;
using System.Data.Common;
using System.Threading;
using LinqToDB.Interceptors;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Runs an action once, right before the first EXISTS query on <c>tableName</c> goes to the database. Unlike
/// <see cref="BeforeUpdateInterceptor"/> it fires after the transaction's first consistent read, so a concurrent request started by the action
/// commits after the read snapshot was taken.
/// </summary>
public class BeforeExistsQueryInterceptor : CommandInterceptor {
    private readonly string _tableName;
    private Action? _action;

    public BeforeExistsQueryInterceptor(string tableName, Action action) {
        _tableName = tableName;
        _action = action;
    }

    public override DbCommand CommandInitialized(CommandEventData eventData, DbCommand command) {
        if (!IsExistsQueryOnTable(command.CommandText))
            return command;

        var action = Interlocked.Exchange(ref _action, null);
        action?.Invoke();
        return command;
    }

    private bool IsExistsQueryOnTable(string commandText) {
        if (!commandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!commandText.Contains("EXISTS", StringComparison.OrdinalIgnoreCase))
            return false;
        return commandText.Contains($"`{_tableName}`", StringComparison.Ordinal);
    }
}
