using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public class MumbleConnectionFactory : IMumbleConnectionFactory {
    private readonly ILoggerFactory _loggerFactory;

    public MumbleConnectionFactory(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
    }

    public IMumbleConnection Create() {
        return new MumbleConnection(_loggerFactory.CreateLogger<MumbleConnection>(), MumbleConnectionTimings.Default);
    }
}
