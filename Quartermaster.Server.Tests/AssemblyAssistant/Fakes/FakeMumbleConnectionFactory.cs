using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

public class FakeMumbleConnectionFactory : IMumbleConnectionFactory {
    public FakeMumbleConnection Connection { get; private set; } = new();
    public int Created { get; private set; }

    public IMumbleConnection Create() {
        if (Created > 0)
            Connection = new FakeMumbleConnection();

        Created++;
        return Connection;
    }
}
