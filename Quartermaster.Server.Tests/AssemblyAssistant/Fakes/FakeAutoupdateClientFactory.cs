using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

/// <summary>Autoupdate clients whose stream delivers one fixed key map and then stays open.</summary>
public class FakeAutoupdateClientFactory : IOpenSlidesAutoupdateClientFactory {
    private readonly string _map;

    public FakeAutoupdateClientFactory(string map) {
        _map = string.Join("", map.Split('\n').Select(l => l.Trim()));
    }

    public OpenSlidesAutoupdateClient Create() {
        var handler = new StreamingHandler();
        handler.Enqueue(HttpStatusCode.OK, [_map], keepOpen: true);
        var timings = new OpenSlidesAutoupdateTimings(TimeSpan.FromMinutes(1), [TimeSpan.FromMilliseconds(50)]);
        return new OpenSlidesAutoupdateClient(new HttpClient(handler), new FakeOpenSlidesClient(), NullLogger<OpenSlidesAutoupdateClient>.Instance, timings);
    }
}
