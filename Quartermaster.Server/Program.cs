using System;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using FastEndpoints;
using FluentMigrator.Runner;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using LinqToDB.AspNet;
using LinqToDB;
using Quartermaster.Data.Migrations;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Email;
using Quartermaster.Server.Members;
using Quartermaster.Server.Security;

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
        builder.Services.Configure<ForwardedHeadersSettings>(builder.Configuration.GetSection("ForwardedHeaders"));
        ConfigureRateLimiter(builder);
#if DEBUG
        builder.Services.Configure<RootAccountSettings>(builder.Configuration.GetSection("RootAccountSettings"));
#endif

        builder.Services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString")!));
        DbContext.AddRepositories(builder.Services);

        // I18n: load the German translation file from wwwroot at startup. The
        // same file is also served as a static asset at /i18n/de.json so that
        // the Blazor WASM client and external API consumers can fetch it.
        // Single source of truth, no embedding.
        builder.Services.AddSingleton<I18nService>(_ => {
            var path = System.IO.Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "i18n", "de.json");
            var json = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
            return new I18nService(json);
        });

        builder.Services.AddSingleton<Quartermaster.Server.AdministrativeDivisions.AdminDivisionImportService>();
        builder.Services.AddHostedService<Quartermaster.Server.AdministrativeDivisions.AdminDivisionImportHostedService>();

        builder.Services.AddSingleton<MemberImportService>();
        builder.Services.AddHostedService<MemberImportHostedService>();

        builder.Services.AddScoped<RetentionAnonymizationService>();
        builder.Services.AddHostedService<RetentionAnonymizationHostedService>();

        builder.Services.AddSingleton(Channel.CreateUnbounded<EmailMessage>());
        builder.Services.AddScoped<EmailService>();
        builder.Services.AddHostedService<EmailSendingBackgroundService>();
        builder.Services.AddScoped<Quartermaster.Server.Events.ChecklistItemExecutor>();
        builder.Services.AddScoped<Quartermaster.Server.Meetings.MeetingLifecycleService>();

        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();

        builder.Services.AddValidatorsFromAssembly(typeof(LoginRequest).Assembly,
            filter: x => x.ValidatorType.BaseType?.GetGenericTypeDefinition() != typeof(Validator<>));
        builder.Services.AddFastEndpoints();
        builder.Services.AddSignalR();
        builder.Services.AddScoped<Quartermaster.Server.Meetings.IMeetingNotifier, Quartermaster.Server.Meetings.MeetingNotifier>();
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
        ConfigureForwardedHeaders(app);

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
        app.UseRateLimiter();

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

        // Antiforgery validation must run after authentication so identity-bound CSRF tokens
        // match the user the cookie identifies (anonymous-context validation false-rejects).
        app.UseMiddleware<Quartermaster.Server.Antiforgery.AntiforgeryMiddleware>();
        app.UseAuthorization();
        app.UseFastEndpoints(c => {
            c.Errors.UseProblemDetails();
        });

#pragma warning disable ASP0014 // MapFallbackToFile does not exist as direct mapping.
        app.UseEndpoints(ep => {
            ep.MapStaticAssets();
            ep.MapHub<Quartermaster.Server.Meetings.MeetingHub>("/hubs/meeting");
            ep.MapFallbackToFile("index.html");
        });
#pragma warning restore ASP0014
    }

    public const string AnonymousCreateRateLimitPolicy = "anonymous-create";

    // Safety fallbacks if the Option is missing, unparseable, or non-positive — the
    // resolver guards against a fat-fingered admin saving "" or "0" and silently
    // disabling all anonymous signups.
    private const int FallbackAnonymousCreatePermits = 5;
    private const int FallbackAnonymousCreateWindowMinutes = 10;

    /// <summary>
    /// Per-IP fixed-window throttle shared across the anonymous POST endpoints
    /// (MotionCreate, MembershipApplicationCreate, DueSelectionCreate). Sharing one bucket
    /// stops an attacker from multiplying their effective rate across endpoints. Values
    /// resolve from <c>OptionRepository</c> (admin-tunable at runtime — takes effect for
    /// new IP partitions immediately and for active partitions after their window resets).
    /// </summary>
    private static void ConfigureRateLimiter(WebApplicationBuilder builder) {
        builder.Services.AddRateLimiter(options => {
            options.RejectionStatusCode = 429;
            options.AddPolicy(AnonymousCreateRateLimitPolicy, httpContext => {
                var optionRepo = httpContext.RequestServices.GetRequiredService<Quartermaster.Data.Options.OptionRepository>();
                var chapterRepo = httpContext.RequestServices.GetRequiredService<Quartermaster.Data.Chapters.ChapterRepository>();

                var permits = ResolvePositiveInt(optionRepo, chapterRepo,
                    "auth.ratelimit.anonymous_create_permits", FallbackAnonymousCreatePermits);
                var windowMinutes = ResolvePositiveInt(optionRepo, chapterRepo,
                    "auth.ratelimit.anonymous_create_window_minutes", FallbackAnonymousCreateWindowMinutes);

                var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
            });
        });
    }

    private static int ResolvePositiveInt(
        Quartermaster.Data.Options.OptionRepository options,
        Quartermaster.Data.Chapters.ChapterRepository chapters,
        string identifier, int fallback) {
        var raw = options.ResolveValue(identifier, null, chapters);
        if (int.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;
        return fallback;
    }

    /// <summary>
    /// Activates X-Forwarded-* processing only for the proxies/networks the deployer
    /// explicitly trusts. Empty configuration ⇒ headers are ignored entirely, and
    /// <c>HttpContext.Connection.RemoteIpAddress</c> stays the authoritative client IP.
    /// </summary>
    private static void ConfigureForwardedHeaders(WebApplication app) {
        var settings = app.Services.GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value;
        if (settings.KnownProxies.Length == 0 && settings.KnownNetworks.Length == 0)
            return;

        var options = new ForwardedHeadersOptions {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        // Defaults are loopback-only; clear so the configured set is the entire trust list.
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        foreach (var proxy in settings.KnownProxies) {
            if (IPAddress.TryParse(proxy, out var ip))
                options.KnownProxies.Add(ip);
        }
        foreach (var network in settings.KnownNetworks) {
            var parsed = ParseNetwork(network);
            if (parsed != null)
                options.KnownNetworks.Add(parsed);
        }

        app.UseForwardedHeaders(options);
    }

    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork? ParseNetwork(string cidr) {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
            return null;
        if (!IPAddress.TryParse(parts[0], out var prefix))
            return null;
        if (!int.TryParse(parts[1], out var length))
            return null;
        return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length);
    }
}
