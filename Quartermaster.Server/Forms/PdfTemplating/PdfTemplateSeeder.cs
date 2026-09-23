using System;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Data.Forms.PdfTemplates;

namespace Quartermaster.Server.Forms.PdfTemplating;

public static class PdfTemplateSeeder {
    public static void Seed(IServiceProvider services) {
        using var scope = services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<PdfTemplateRepository>();
        var now = DateTime.UtcNow;
        foreach (var (key, body) in PdfTemplateDefaults.ByKey) {
            repo.EnsureSeeded(key, body, now);
        }
    }
}
