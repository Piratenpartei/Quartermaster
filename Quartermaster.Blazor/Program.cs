using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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

        builder.Services.AddSingleton<ClientConfigService>();
        builder.Services.AddSingleton<ToastService>();

        await builder.Build().RunAsync();
    }
}