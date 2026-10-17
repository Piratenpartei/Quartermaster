using System;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public class MumbleProtocolException : Exception {
    public MumbleProtocolException(string message) : base(message) { }
}
