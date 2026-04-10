using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor;

public static class Program {
    public static async Task Main(string[] args) {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddSingleton<AppStateService>();

        builder.Services.AddTransient<Quartermaster.Blazor.Http.CsrfDelegatingHandler>();
        builder.Services.AddHttpClient("Default", client => {
            client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        }).AddHttpMessageHandler<Quartermaster.Blazor.Http.CsrfDelegatingHandler>();
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Default"));

        // I18n: fetch the German translation file from /i18n/de.json (served by
        // the server as a static asset from wwwroot). Single source of truth for
        // both server and client. If the fetch fails (offline, server down) the
        // app still boots — the I18nService falls back to raw keys for any
        // missing translation.
        using (var bootHttp = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }) {
            var json = "";
            try {
                json = await bootHttp.GetStringAsync("i18n/de.json");
            } catch {
                // Translations unavailable — fall back to raw keys.
            }
            builder.Services.AddSingleton(new I18nService(json));
        }

        builder.Services.AddScoped<ClientConfigService>();
        builder.Services.AddScoped<ToastService>();
        builder.Services.AddScoped<AuthService>();

        await builder.Build().RunAsync();
    }
}