using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartermaster.Data;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Starts a real Kestrel host (not TestServer) so that external processes like Playwright
/// can navigate to it. Builds the app via <see cref="Program.ConfigureServices"/> and
/// <see cref="Program.ConfigureMiddleware"/> — the same configuration the production host uses,
/// minus HTTPS redirection, migrations, and hosted background services.
/// Disposes the host on <see cref="Dispose"/>.
/// </summary>
public sealed class E2ETestFactory : IDisposable {
    private readonly WebApplication _app;
    public string BaseUrl { get; }

    public E2ETestFactory(string connectionString) {
        var contentRoot = FindServerContentRoot();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = contentRoot,
            ApplicationName = "Quartermaster.Server",
            EnvironmentName = "Development"
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DatabaseSettings:ConnectionString"] = connectionString,
            ["RootAccountSettings:Email"] = "",
            ["RootAccountSettings:Password"] = ""
        });

        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        Program.ConfigureServices(builder);

        // Strip hosted services — tests don't need background polling/SMTP.
        var hosted = builder.Services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        foreach (var d in hosted)
            builder.Services.Remove(d);

        _app = builder.Build();

        DbContext.SupplementDefaults(_app.Services);
        Program.ConfigureMiddleware(_app);

        _app.Start();

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Server started without addresses feature");
        BaseUrl = addresses.Addresses.First();
    }

    public void Dispose() {
        _app.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_app).Dispose();
    }

    /// <summary>
    /// Locates the Quartermaster.Server project's bin output directory by searching
    /// for its static-web-assets manifest. Needed because the test project's bin does
    /// not contain the Blazor WASM framework files.
    /// </summary>
    private static string FindServerContentRoot() {
        const string manifestName = "Quartermaster.Server.staticwebassets.endpoints.json";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null) {
            // Walk up until we find a directory containing the solution file, then
            // navigate into Quartermaster.Server/bin/<configuration>/<tfm>/.
            if (File.Exists(Path.Combine(dir.FullName, "Quartermaster.sln"))) {
                var serverBin = Path.Combine(
                    dir.FullName, "Quartermaster.Server", "bin",
                    GetConfiguration(), GetTargetFramework());
                if (File.Exists(Path.Combine(serverBin, manifestName))) {
                    return serverBin;
                }
                throw new DirectoryNotFoundException(
                    $"Could not find server bin directory. Expected at: {serverBin}");
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate the repository root (Quartermaster.sln not found in any ancestor directory)");
    }

    private static string GetConfiguration() {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string GetTargetFramework() => "net10.0";
}
