using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using LinqToDB.AspNet;
using LinqToDB;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Migrations;
using Quartermaster.Data.Options;
using Quartermaster.Data.Users;
using Quartermaster.Server.AdministrativeDivisions;
using Quartermaster.Server.Antiforgery;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Cli;
using Quartermaster.Server.Email;
using Quartermaster.Server.Rendering;
using Quartermaster.Server.Settings;
using Quartermaster.Server.Settings.Checks;
using Quartermaster.Server.DueSelector;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.Motions;
using Quartermaster.Server.Submissions;
using Quartermaster.Server.Events;
using Quartermaster.Server.Meetings;
using Quartermaster.Server.Members;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Notifications.Telegram;
using Quartermaster.Server.Security;

namespace Quartermaster.Server;

public partial class Program {
    public static void Main(string[] args) {
        if (args.Length > 0 && args[0] == "init-admin") {
            Environment.Exit(AdminInitCommand.Execute(args));
            return;
        }

        var builder = WebApplication.CreateBuilder(args);

        // QuestPDF is MIT-licensed Community edition (free for orgs under €1M annual revenue).
        // License must be set before any QuestPDF API is used.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        ConfigureServices(builder);

        builder.Services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                // Read on first use: configuration overrides (the test hosts' own database) only apply once the app is built.
                rb.AddMySql8().WithGlobalConnectionString(provider => provider.GetRequiredService<IConfiguration>().GetValue<string>("DatabaseSettings:ConnectionString")!)
                    .ScanIn(typeof(M001_InitialStructureMigration).Assembly).For.Migrations();
            });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope()) {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            migrator.MigrateUp();
        }

        DbContext.SupplementDefaults(app.Services);
        Forms.PdfTemplating.PdfTemplateSeeder.Seed(app.Services);
#if DEBUG
        SeedRootAccountFromConfig(app.Services);
#endif

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

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<PermissionContext>();
        builder.Services.AddScoped<PublicForms.PublicFormSettings>();

        // I18n: load the German translation file from wwwroot at startup. The
        // same file is also served as a static asset at /i18n/de.json so that
        // the Blazor WASM client and external API consumers can fetch it.
        // Single source of truth, no embedding.
        builder.Services.AddSingleton<I18nService>(_ => {
            var path = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "i18n", "de.json");
            var json = File.Exists(path) ? File.ReadAllText(path) : "";
            return new I18nService(json);
        });

        builder.Services.AddSingleton<Changelog.ChangelogService>();

        builder.Services.AddSingleton<Banking.BankCodeBicLookup>();
        builder.Services.AddSingleton<Forms.FormAssets>();
        builder.Services.AddSingleton<Forms.PdfTemplating.PdfTemplateEngine>();
        builder.Services.AddScoped<Forms.ExpenseReimbursements.ExpenseReimbursementPdfService>();
        builder.Services.AddSingleton<Forms.PdfTemplating.IFormMockData, Forms.ExpenseReimbursements.ExpenseReimbursementMockData>();

        builder.Services.AddSingleton<NewsPosts.Programme.ProgrammeIndex>();
        builder.Services.AddScoped(sp => NewsPosts.NewsPostsSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddScoped<NewsPosts.Mistral.IMistralClient>(sp => new NewsPosts.Mistral.HttpMistralClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<NewsPosts.NewsPostsSettings>(),
            sp.GetRequiredService<ILogger<NewsPosts.Mistral.HttpMistralClient>>()));
        builder.Services.AddScoped<NewsPosts.Programme.IWikiClient>(sp => new NewsPosts.Programme.HttpWikiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        builder.Services.AddScoped<NewsPosts.Programme.ProgrammeSyncService>();
        builder.Services.AddHostedService<NewsPosts.Programme.ProgrammeSyncHostedService>();
        builder.Services.AddScoped<NewsPosts.Feeds.IFeedFetcher>(sp => new NewsPosts.Feeds.HttpFeedFetcher(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        builder.Services.AddScoped<NewsPosts.Feeds.NewsFeedPoller>();
        builder.Services.AddScoped<NewsPosts.Generation.RelevanceTriage>();
        builder.Services.AddScoped<NewsPosts.Generation.CandidateGenerator>();
        builder.Services.AddScoped<NewsPosts.Pipeline.NewsPipeline>();
        builder.Services.AddHostedService<NewsPosts.Pipeline.NewsPipelineHostedService>();
        builder.Services.AddScoped<NewsPosts.Postiz.IPostizClient>(sp => new NewsPosts.Postiz.HttpPostizClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<NewsPosts.NewsPostsSettings>(),
            sp.GetRequiredService<ILogger<NewsPosts.Postiz.HttpPostizClient>>()));

        builder.Services.AddSingleton<Accreditation.KeycloakTokenProvider>();
        builder.Services.AddScoped(sp => Accreditation.KeycloakSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddScoped<Accreditation.IKeycloakAdminClient>(sp => new Accreditation.HttpKeycloakAdminClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<Accreditation.KeycloakSettings>(),
            sp.GetRequiredService<Accreditation.KeycloakTokenProvider>()));
        builder.Services.AddScoped<Accreditation.TemporaryAccountChecker>();

        builder.Services.AddScoped(sp => AssemblyMotions.Wiki.WikiBotSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddSingleton<AssemblyMotions.Wiki.IWikiBotClient>(sp => new AssemblyMotions.Wiki.HttpWikiBotClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("wiki"),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<AssemblyMotions.Wiki.HttpWikiBotClient>>()));

        builder.Services.AddScoped(sp => AssemblyMotions.Forum.DiscourseSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddSingleton<Forwarding.Redmine.IRedmineClient>(sp => new Forwarding.Redmine.HttpRedmineClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("redmine"),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<Forwarding.Redmine.HttpRedmineClient>>()));
        builder.Services.AddSingleton<AssemblyMotions.Forum.IDiscourseClient>(sp => new AssemblyMotions.Forum.HttpDiscourseClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("discourse"),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<AssemblyMotions.Forum.HttpDiscourseClient>>()));
        builder.Services.AddScoped(sp => AssemblyMotions.OpenSlides.OpenSlidesSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddSingleton<AssemblyMotions.OpenSlides.IOpenSlidesClient>(sp => new AssemblyMotions.OpenSlides.HttpOpenSlidesClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("openslides"),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<AssemblyMotions.OpenSlides.HttpOpenSlidesClient>>()));

        builder.Services.AddScoped<AssemblyMotionNotifier>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionEligibility>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionAccess>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionService>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionCommissionService>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionInquiryService>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionCompetitionService>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionUpvoteService>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionResultService>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionCompetitionViewBuilder>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionDetailBuilder>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionMemberActions>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionCommissionActions>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionPortalSettingsBuilder>();
        builder.Services.AddScoped<AssemblyMotions.AssemblyMotionExportBuilder>();

        builder.Services.AddSingleton<AdministrativeDivisionSearchIndex>();
        builder.Services.AddSingleton<AdminDivisionImportService>();
        builder.Services.AddSingleton<AdministrationAddressImportService>();
        builder.Services.AddHostedService<AdminDivisionImportHostedService>();

        builder.Services.AddSingleton<MemberImportService>();
        builder.Services.AddHostedService<MemberImportHostedService>();

        builder.Services.AddScoped<RetentionAnonymizationService>();
        builder.Services.AddHostedService<RetentionAnonymizationHostedService>();
        builder.Services.AddHostedService<AssemblyMotions.AssemblyMotionDeadlineHostedService>();
        builder.Services.AddScoped<AssemblyMotions.Wiki.AssemblyMotionWikiSync>();
        builder.Services.AddScoped<AssemblyMotions.Wiki.AssemblyMotionWikiReconcile>();
        builder.Services.AddScoped<AssemblyMotions.Wiki.AssemblyMotionWikiConnectionCheck>();
        builder.Services.AddScoped<AssemblyMotions.Wiki.AssemblyMotionWikiResync>();
        builder.Services.AddScoped<AssemblyMotions.Forum.AssemblyMotionForumSync>();
        builder.Services.AddScoped<Forwarding.ForwardingSettingsResolver>();
        builder.Services.AddScoped<Forwarding.ForwardingTicketRenderer>();
        builder.Services.AddScoped<Forwarding.ForwardingIntake>();
        builder.Services.AddScoped<Forwarding.ForwardingOutcomeHook>();
        builder.Services.AddScoped<Forwarding.ForwardingFailureMailer>();
        builder.Services.AddScoped<Forwarding.ForwardingCaseActions>();
        builder.Services.AddScoped<Forwarding.ForwardingJob>();
        builder.Services.AddScoped<AssemblyMotions.Forum.AssemblyMotionForumConnectionCheck>();
        builder.Services.AddScoped<AssemblyMotions.Forum.AssemblyMotionForumResync>();
        builder.Services.AddScoped<SupportSignatures.SignatureCampaignAccess>();
        builder.Services.AddScoped<SupportSignatures.SignatureCampaignDtoBuilder>();
        builder.Services.AddScoped<SupportSignatures.SignatureIntakeService>();
        builder.Services.AddScoped<SupportSignatures.SignatureOverviewBuilder>();
        builder.Services.AddScoped<SupportSignatures.SignatureShipmentService>();
        builder.Services.AddScoped<SupportSignatures.CoverLetterRenderer>();
        builder.Services.AddScoped<SupportSignatures.MemberSignatureCampaigns>();
        builder.Services.AddScoped<SupportSignatures.Endpoints.Shipments.ShipmentCampaignLookup>();
        builder.Services.AddSingleton<SupportSignatures.SupportSignatureFormStorage>();
        builder.Services.AddScoped<SupportSignatures.CertificationOfficeSuggester>();
        builder.Services.AddScoped<SupportSignatures.Endpoints.Offices.CertificationOfficeWriter>();
        builder.Services.AddScoped<SupportSignatures.CustomOfficeImporter>();
        builder.Services.AddScoped<AssemblyMotions.OpenSlides.AssemblyMotionOpenSlidesPush>();

        builder.Services.AddSingleton<AssemblyAssistant.Mumble.IMumbleConnectionFactory, AssemblyAssistant.Mumble.MumbleConnectionFactory>();
        builder.Services.AddScoped<AssemblyAssistant.Mumble.MumbleConnectionCheck>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AssemblyAssistant.Session.IAssemblyPublisher, AssemblyAssistant.HubAssemblyPublisher>();
        builder.Services.AddSingleton<AssemblyAssistant.OpenSlides.IOpenSlidesAutoupdateClientFactory, AssemblyAssistant.OpenSlides.OpenSlidesAutoupdateClientFactory>();
        builder.Services.AddSingleton<AssemblyAssistant.Session.AssemblySessionHost>();
        builder.Services.AddScoped<AssemblyAssistant.Export.AssemblyLogExport>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AssemblyAssistant.Session.AssemblySessionHost>());
        builder.Services.AddHostedService<AssemblyMotions.Wiki.AssemblyMotionWikiSyncHostedService>();
        builder.Services.AddHostedService<AssemblyMotions.Wiki.AssemblyMotionWikiReconcileHostedService>();
        builder.Services.AddHostedService<AssemblyMotions.Forum.AssemblyMotionForumSyncHostedService>();
        builder.Services.AddHostedService<Forwarding.ForwardingJobHostedService>();

        builder.Services.AddSingleton(Channel.CreateUnbounded<EmailMessage>());
        builder.Services.AddScoped<EmailService>();
        builder.Services.AddScoped<SmtpTestService>();

        builder.Services.AddSingleton<Receipts.ReceiptStorage>();
        builder.Services.AddScoped<Receipts.ReceiptLimits>();
        builder.Services.AddScoped<TravelExpenses.TravelExpenseService>();
        builder.Services.AddScoped<Users.KeycloakDepartmentChapterAssigner>();
        builder.Services.AddScoped<Users.KeycloakUserImporter>();
        builder.Services.AddScoped<TravelExpenses.TravelExpensePdfService>();
        builder.Services.AddSingleton<TravelExpenses.ReceiptAppender>();
        builder.Services.AddSingleton<Forms.PdfTemplating.IFormMockData, TravelExpenses.TravelExpenseMockData>();

        builder.Services.AddScoped<ISettingsCheck, TelegramSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, MemberImportSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, KeycloakSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, WikiSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, OpenSlidesSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, ForumSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, MistralSettingsCheck>();
        builder.Services.AddScoped<ISettingsCheck, PostizSettingsCheck>();

        builder.Services.AddHostedService<EmailSendingBackgroundService>();
        builder.Services.AddHostedService<MdnPollerHostedService>();
        builder.Services.AddHostedService<MarkdownWarmupHostedService>();

        builder.Services.AddScoped<EmailMessageChannel>();
        builder.Services.AddSingleton<TelegramBotClientCache>();
        builder.Services.AddScoped<TelegramBotClientFactory>();
        builder.Services.AddScoped<TelegramMessageChannel>();
        builder.Services.AddScoped<TelegramUpdateHandler>();
        builder.Services.AddHostedService<TelegramReceiverBackgroundService>();
        builder.Services.AddScoped<PdfMessageChannel>();
        builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<EmailMessageChannel>());
        builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<TelegramMessageChannel>());
        builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<PdfMessageChannel>());
        builder.Services.AddScoped<MessageChannelRegistry>();

        builder.Services.AddScoped<NotificationTemplateGlobals>();
        builder.Services.AddScoped<IRecipientResolver, MotionSubmittedRecipientResolver>();
        builder.Services.AddScoped<IRecipientResolver, ApplicationSubmittedRecipientResolver>();
        builder.Services.AddScoped<IRecipientResolver, DueSelectionSubmittedRecipientResolver>();
        foreach (var triggerId in AssemblyMotionRecipientResolver.TriggerIds)
            builder.Services.AddScoped<IRecipientResolver>(sp => new AssemblyMotionRecipientResolver(triggerId, sp.GetRequiredService<DbContext>()));
        builder.Services.AddScoped<NotificationDispatcher>();
        builder.Services.AddSingleton<ChannelNotificationDispatchQueue>();
        builder.Services.AddSingleton<INotificationDispatchQueue>(sp => sp.GetRequiredService<ChannelNotificationDispatchQueue>());
        builder.Services.AddHostedService<NotificationDispatchBackgroundService>();

        builder.Services.AddScoped<SubmissionConfirmationEmailService>();
        builder.Services.AddScoped<SubmissionIntakeService>();
        builder.Services.AddScoped<SubmissionMaterializer>();
        builder.Services.AddScoped<MembershipApplicationMailService>();
        builder.Services.AddScoped<ApplicationReviewService>();
        builder.Services.AddScoped<DueSelectionMailService>();
        builder.Services.AddScoped<MotionResolutionDecisionMailer>();
        builder.Services.AddHostedService<PendingSubmissionCleanupHostedService>();

        builder.Services.AddScoped<ChecklistItemExecutor>();
        builder.Services.AddScoped<MeetingLifecycleService>();

        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient("wiki")
            .ConfigurePrimaryHttpMessageHandler(AssemblyMotions.Wiki.HttpWikiBotClient.CreateHandler);
        builder.Services.AddHttpClient("redmine")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                UseCookies = false
            });
        builder.Services.AddHttpClient("discourse")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                UseCookies = false
            });
        builder.Services.AddHttpClient("openslides")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                UseCookies = false
            });
        builder.Services.AddHttpClient(AssemblyAssistant.OpenSlides.OpenSlidesAutoupdateClientFactory.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                AllowAutoRedirect = false,
                UseCookies = false
            });
        // The timeout must stay above the receiver's 25 second long poll.
        builder.Services.AddHttpClient(TelegramBotClientFactory.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(40))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
        builder.Services.AddMemoryCache();

        builder.Services.AddValidatorsFromAssembly(typeof(LoginRequest).Assembly,
            filter: x => x.ValidatorType.BaseType?.GetGenericTypeDefinition() != typeof(Validator<>));
        builder.Services.AddFastEndpoints();
        builder.Services.AddSignalR();
        builder.Services.AddScoped<IMeetingNotifier, MeetingNotifier>();
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

        app.UseMiddleware<SecurityHeadersMiddleware>();

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

        app.UseAuthentication();
        app.Use(async (context, next) => {
            if (context.User.Identity?.IsAuthenticated == true) {
                var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim != null && Guid.TryParse(idClaim.Value, out var userId)) {
                    var auditLog = context.RequestServices.GetRequiredService<AuditLogRepository>();
                    auditLog.SetCurrentUser(userId, context.User.Identity.Name ?? "System");
                }
            }
            await next();
        });

        // After authentication, so the per-account due selection limit can see who is calling.
        app.UseRateLimiter();

        // Antiforgery validation must run after authentication so identity-bound CSRF tokens
        // match the user the cookie identifies (anonymous-context validation false-rejects).
        app.UseMiddleware<AntiforgeryMiddleware>();
        app.UseAuthorization();
        app.UseFastEndpoints(c => {
            c.Errors.UseProblemDetails();
        });

#pragma warning disable ASP0014 // MapFallbackToFile does not exist as direct mapping.
        app.UseEndpoints(ep => {
            ep.MapStaticAssets();
            ep.MapHub<MeetingHub>("/hubs/meeting");
            ep.MapHub<AssemblyAssistant.AssemblyHub>(Api.AssemblyAssistant.AssemblyHubMethods.HubPath);
            foreach (var (legacy, current) in LegacyRoutes) {
                ep.MapGet(legacy, (HttpContext context) => Results.Redirect(current + context.Request.QueryString, permanent: true))
                    .AllowAnonymous();
            }
            ep.MapFallbackToFile("index.html");
        });
#pragma warning restore ASP0014
    }

    /// <summary>Pages that used to live under German routes; the old addresses redirect so bookmarks keep working.</summary>
    private static readonly (string Legacy, string Current)[] LegacyRoutes = [
        ("/Account/Benachrichtigungen", "/Account/Notifications"),
        ("/Administration/Akkreditierung", "/Administration/Accreditation")
    ];

    public const string AnonymousCreateRateLimitPolicy = "anonymous-create";
    public const string DueSelectionRateLimitPolicy = "due-selection";

    // Safety fallbacks if the Option is missing, unparseable, or non-positive — the
    // resolver guards against a fat-fingered admin saving "" or "0" and silently
    // disabling all anonymous signups.
    private const int FallbackAnonymousCreatePermits = 5;
    private const int FallbackAnonymousCreateWindowMinutes = 10;
    private const int FallbackDueSelectionPermits = 5;
    private const int FallbackDueSelectionWindowMinutes = 1440;

    /// <summary>
    /// Per-IP fixed-window throttle shared across the anonymous POST endpoints
    /// (MotionCreate, MembershipApplicationCreate, ExpenseReimbursementRender, TravelExpenseRender), and a per-account one for
    /// DueSelectionCreate, which requires login and creates a federal Redmine ticket per submission. Sharing one bucket
    /// stops an attacker from multiplying their effective rate across endpoints. Values
    /// resolve from <c>OptionRepository</c> (admin-tunable at runtime — takes effect for
    /// new IP partitions immediately and for active partitions after their window resets).
    /// </summary>
    private static void ConfigureRateLimiter(WebApplicationBuilder builder) {
        builder.Services.AddRateLimiter(options => {
            options.RejectionStatusCode = 429;
            options.AddPolicy(AnonymousCreateRateLimitPolicy, httpContext => {
                var optionRepo = httpContext.RequestServices.GetRequiredService<OptionRepository>();
                var chapterRepo = httpContext.RequestServices.GetRequiredService<ChapterRepository>();

                var permits = ResolvePositiveInt(optionRepo, chapterRepo,
                    "auth.ratelimit.anonymous_create_permits", FallbackAnonymousCreatePermits);
                var windowMinutes = ResolvePositiveInt(optionRepo, chapterRepo,
                    "auth.ratelimit.anonymous_create_window_minutes", FallbackAnonymousCreateWindowMinutes);

                var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return FixedWindow(key, permits, windowMinutes);
            });
            options.AddPolicy(DueSelectionRateLimitPolicy, httpContext => {
                var optionRepo = httpContext.RequestServices.GetRequiredService<OptionRepository>();
                var chapterRepo = httpContext.RequestServices.GetRequiredService<ChapterRepository>();

                var permits = ResolvePositiveInt(optionRepo, chapterRepo,
                    "auth.ratelimit.due_selection_permits", FallbackDueSelectionPermits);
                var windowMinutes = ResolvePositiveInt(optionRepo, chapterRepo,
                    "auth.ratelimit.due_selection_window_minutes", FallbackDueSelectionWindowMinutes);

                // The endpoint answers anonymous callers with 401; they still share one per-IP bucket so they cannot flood it.
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var key = userId != null ? $"user:{userId}" : $"ip:{httpContext.Connection.RemoteIpAddress}";
                return FixedWindow(key, permits, windowMinutes);
            });
        });
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permits, int windowMinutes) {
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(windowMinutes),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    private static int ResolvePositiveInt(
        OptionRepository options,
        ChapterRepository chapters,
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

#if DEBUG
    private static void SeedRootAccountFromConfig(IServiceProvider services) {
        var rootSettings = services.GetRequiredService<IOptions<RootAccountSettings>>().Value;
        if (string.IsNullOrEmpty(rootSettings.Username) || string.IsNullOrEmpty(rootSettings.Password))
            return;
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<UserRepository>().SupplementDefaults(rootSettings);
    }
#endif

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
