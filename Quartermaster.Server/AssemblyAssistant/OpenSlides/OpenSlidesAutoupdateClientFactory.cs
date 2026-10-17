using System.Net.Http;
using Microsoft.Extensions.Logging;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

public class OpenSlidesAutoupdateClientFactory : IOpenSlidesAutoupdateClientFactory {
    public const string HttpClientName = "openslides-stream";

    private readonly IHttpClientFactory _httpClients;
    private readonly IOpenSlidesClient _client;
    private readonly ILoggerFactory _loggers;

    public OpenSlidesAutoupdateClientFactory(IHttpClientFactory httpClients, IOpenSlidesClient client, ILoggerFactory loggers) {
        _httpClients = httpClients;
        _client = client;
        _loggers = loggers;
    }

    public OpenSlidesAutoupdateClient Create() {
        return new OpenSlidesAutoupdateClient(_httpClients.CreateClient(HttpClientName), _client,
            _loggers.CreateLogger<OpenSlidesAutoupdateClient>(), OpenSlidesAutoupdateTimings.Default);
    }
}
