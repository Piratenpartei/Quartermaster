using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Options;

public static class TemplateMockDataProvider {
    public static System.Collections.Generic.Dictionary<string, object> GetMockData(string templateModels)
        => Api.Rendering.TemplateMockDataProvider.GetMockData(templateModels);
}
