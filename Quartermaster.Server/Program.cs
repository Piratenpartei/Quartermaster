using System;
using System.Threading.Channels;
using FastEndpoints;
using FluentMigrator.Runner;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using LinqToDB.AspNet;
using LinqToDB;
using Quartermaster.Data.Migrations;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Email;
using Quartermaster.Server.Members;

namespace Quartermaster.Server;

public partial class Program {
    public static void Main(string[] args) {
        if (args.Length > 0 && args[0] == "init-admin") {
            System.Environment.Exit(Quartermaster.Server.Cli.AdminInitCommand.Execute(args));
            return;
        }

        var builder = WebApplication.CreateBuilder(args);

        // QuestPDF is MIT-licensed Community edition (free for orgs under €1M annual revenue).
        // License must be set before any QuestPDF API is used.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        ConfigureServices(builder);

        builder.Services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                rb.AddMySql8().WithGlobalConnectionString(builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString"))
                    .ScanIn(typeof(M001_InitialStructureMigration).Assembly).For.Migrations();
            });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope()) {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            migrator.MigrateUp();
        }

        DbContext.SupplementDefaults(app.Services);

        app.UseHttpsRedirection();
        ConfigureMiddleware(app);

        app.Run();
    }

    /// <summary>
    /// Registers services used by the production app. Does not register the FluentMigrator
    /// runner (tests handle migrations separately) or the HttpsRedirection middleware.
    /// Both <see cref="Main"/> and the E2E test factory call this method.
    /// </summary>
    public static void ConfigureServices(WebApplicationBuilder builder) {
        builder.Services.AddAuthentication(TokenAuthenticationHandlerOptions.DefaultScheme)
            .AddScheme<TokenAuthenticationHandlerOptions, TokenAuthenticationHandler>(
                TokenAuthenticationHandlerOptions.DefaultScheme, null);
        builder.Services.AddAuthorization();

        builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));
#if DEBUG
        builder.Services.Configure<RootAccountSettings>(builder.Configuration.GetSection("RootAccountSettings"));
#endif

        builder.Services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString")!));
        DbContext.AddRepositories(builder.Services);

        builder.Services.AddSingleton<Quartermaster.Server.AdministrativeDivisions.AdminDivisionImportService>();
        builder.Services.AddHostedService<Quartermaster.Server.AdministrativeDivisions.AdminDivisionImportHostedService>();

        builder.Services.AddSingleton<MemberImportService>();
        builder.Services.AddHostedService<MemberImportHostedService>();

        builder.Services.AddSingleton(Channel.CreateUnbounded<EmailMessage>());
        builder.Services.AddScoped<EmailService>();
        builder.Services.AddHostedService<EmailSendingBackgroundService>();
        builder.Services.AddScoped<Quartermaster.Server.Events.ChecklistItemExecutor>();
        builder.Services.AddScoped<Quartermaster.Server.Meetings.MeetingLifecycleService>();

        builder.Services.AddValidatorsFromAssembly(typeof(LoginRequest).Assembly,
            filter: x => x.ValidatorType.BaseType?.GetGenericTypeDefinition() != typeof(Validator<>));
        builder.Services.AddFastEndpoints();
        builder.Services.AddAntiforgery(options => {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = ".Quartermaster.Antiforgery";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
    }

    /// <summary>
    /// Wires up HTTP middleware. Excludes the HTTPS redirection and migration steps
    /// (those are host-specific). Both <see cref="Main"/> and the E2E test factory call this.
    /// </summary>
    public static void ConfigureMiddleware(WebApplication app) {
        app.UseMiddleware<Quartermaster.Server.Security.SecurityHeadersMiddleware>();

        app.UseExceptionHandler(appError => {
            appError.Run(async context => {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new {
                    statusCode = 500,
                    message = "Ein interner Serverfehler ist aufgetreten."
                });
            });
        });

        app.UseRouting();

        app.UseMiddleware<Quartermaster.Server.Antiforgery.AntiforgeryMiddleware>();

        app.UseAuthentication();
        app.Use(async (context, next) => {
            if (context.User.Identity?.IsAuthenticated == true) {
                var idClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim != null && Guid.TryParse(idClaim.Value, out var userId)) {
                    var auditLog = context.RequestServices.GetRequiredService<Quartermaster.Data.AuditLog.AuditLogRepository>();
                    auditLog.SetCurrentUser(userId, context.User.Identity.Name ?? "System");
                }
            }
            await next();
        });
        app.UseAuthorization();
        app.UseFastEndpoints(c => {
            c.Errors.UseProblemDetails();
        });

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

#pragma warning disable ASP0014 // MapFallbackToFile does not exist as direct mapping.
        app.UseEndpoints(ep => ep.MapFallbackToFile("index.html"));
#pragma warning restore ASP0014
    }
}
