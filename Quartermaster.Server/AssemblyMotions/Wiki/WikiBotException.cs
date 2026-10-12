using System;
namespace Quartermaster.Server.AssemblyMotions.Wiki;

public class WikiBotException : Exception {
    public string? Code { get; }

    public WikiBotException(string message, string? code = null, Exception? inner = null)
        : base(message, inner) {
        Code = code;
    }
}
