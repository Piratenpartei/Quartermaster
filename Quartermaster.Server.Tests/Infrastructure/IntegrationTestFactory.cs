using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// In-process test host built on top of <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Swaps the app's connection string to point at the caller's per-worker test database,
/// removes hosted services (admin-div import, member import, email sending) so they
/// don't interfere with tests, and keeps everything else identical to production.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program> {
    private readonly string _connectionString;

    public IntegrationTestFactory(string connectionString) {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) => {
            config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["DatabaseSettings:ConnectionString"] = _connectionString,
                // Disable RootAccountSettings seeding in test environment —
                // tests create their own authenticated users via TestDataBuilder.
                ["RootAccountSettings:Email"] = "",
                ["RootAccountSettings:Password"] = ""
            });
        });

        builder.ConfigureServices(services => {
            // Remove hosted services — they would spin up background work (file polling,
            // SMTP sending) that interferes with tests and may fail without a filesystem
            // or SMTP server.
            RemoveHostedServices(services);
        });
    }

    private static void RemoveHostedServices(IServiceCollection services) {
        var hostedServiceDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();
        foreach (var desc in hostedServiceDescriptors) {
            services.Remove(desc);
        }
    }
}
