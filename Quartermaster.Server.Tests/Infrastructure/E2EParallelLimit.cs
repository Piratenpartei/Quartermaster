using TUnit.Core.Interfaces;

namespace Quartermaster.Server.Tests.Infrastructure;

public class E2EParallelLimit : IParallelLimit {
    public int Limit => 6;
}
