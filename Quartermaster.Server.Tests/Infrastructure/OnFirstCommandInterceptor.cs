using System;
using System.Data.Common;
using LinqToDB.Interceptors;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>Runs an action once, right before the first command of the context it is added to, to put a concurrent write between two reads of the code under test.</summary>
public class OnFirstCommandInterceptor : CommandInterceptor {
    private readonly Action _action;
    private bool _fired;

    public OnFirstCommandInterceptor(Action action) {
        _action = action;
    }

    public override DbCommand CommandInitialized(CommandEventData eventData, DbCommand command) {
        if (!_fired) {
            _fired = true;
            _action();
        }
        return command;
    }
}
