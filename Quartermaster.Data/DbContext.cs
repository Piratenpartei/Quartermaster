using System;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Collab;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Events;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.PdfTemplates;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.Saml;
using Quartermaster.Data.Submissions;
using Quartermaster.Data.Templates;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;

namespace Quartermaster.Data;

public class DbContext : DataConnection {
    public ITable<Token> Tokens => this.GetTable<Token>();
    public ITable<Permission> Permissions => this.GetTable<Permission>();
    public ITable<UserChapterPermission> UserChapterPermissions => this.GetTable<UserChapterPermission>();
    public ITable<UserGlobalPermission> UserGlobalPermissions => this.GetTable<UserGlobalPermission>();
    public ITable<AdministrativeDivision> AdministrativeDivisions => this.GetTable<AdministrativeDivision>();
    public ITable<User> Users => this.GetTable<User>();
    public ITable<DueSelection> DueSelections => this.GetTable<DueSelection>();
    public ITable<Chapter> Chapters => this.GetTable<Chapter>();
    public ITable<ChapterExternalCode> ChapterExternalCodes => this.GetTable<ChapterExternalCode>();
    public ITable<ChapterAdministrativeDivision> ChapterAdministrativeDivisions => this.GetTable<ChapterAdministrativeDivision>();
    public ITable<ChapterMail> ChapterMails => this.GetTable<ChapterMail>();
    public ITable<MembershipApplication> MembershipApplications => this.GetTable<MembershipApplication>();
    public ITable<ChapterOfficer> ChapterOfficers => this.GetTable<ChapterOfficer>();
    public ITable<Motion> Motions => this.GetTable<Motion>();
    public ITable<MotionVote> MotionVotes => this.GetTable<MotionVote>();
    public ITable<SystemOption> SystemOptions => this.GetTable<SystemOption>();
    public ITable<OptionDefinition> OptionDefinitions => this.GetTable<OptionDefinition>();
    public ITable<Template> Templates => this.GetTable<Template>();
    public ITable<Member> Members => this.GetTable<Member>();
    public ITable<MemberImportLog> MemberImportLogs => this.GetTable<MemberImportLog>();
    public ITable<AdminDivisionImportLog> AdminDivisionImportLogs => this.GetTable<AdminDivisionImportLog>();
    public ITable<Event> Events => this.GetTable<Event>();
    public ITable<EventChecklistItem> EventChecklistItems => this.GetTable<EventChecklistItem>();
    public ITable<EventTemplate> EventTemplates => this.GetTable<EventTemplate>();
    public ITable<NotificationLog> NotificationLogs => this.GetTable<NotificationLog>();
    public ITable<UserNotificationPreference> UserNotificationPreferences => this.GetTable<UserNotificationPreference>();
    public ITable<TelegramLinkToken> TelegramLinkTokens => this.GetTable<TelegramLinkToken>();
    public ITable<PendingSubmission> PendingSubmissions => this.GetTable<PendingSubmission>();
    public ITable<AuditEntry> AuditLogs => this.GetTable<AuditEntry>();
    public ITable<LoginAttempt> LoginAttempts => this.GetTable<LoginAttempt>();
    public ITable<Role> Roles => this.GetTable<Role>();
    public ITable<RolePermission> RolePermissions => this.GetTable<RolePermission>();
    public ITable<UserRoleAssignment> UserRoleAssignments => this.GetTable<UserRoleAssignment>();
    public ITable<Meetings.Meeting> Meetings => this.GetTable<Meetings.Meeting>();
    public ITable<Meetings.AgendaItem> AgendaItems => this.GetTable<Meetings.AgendaItem>();
    public ITable<CollabDocument> CollabDocuments => this.GetTable<CollabDocument>();
    public ITable<UsedSamlAssertion> UsedSamlAssertions => this.GetTable<UsedSamlAssertion>();
    public ITable<ExpenseReimbursement> ExpenseReimbursements => this.GetTable<ExpenseReimbursement>();
    public ITable<ExpenseReimbursementLineItem> ExpenseReimbursementLineItems => this.GetTable<ExpenseReimbursementLineItem>();
    public ITable<PdfTemplate> PdfTemplates => this.GetTable<PdfTemplate>();
    public ITable<ProgrammeSource> ProgrammeSources => this.GetTable<ProgrammeSource>();
    public ITable<ProgrammeSection> ProgrammeSections => this.GetTable<ProgrammeSection>();
    public ITable<NewsFeed> NewsFeeds => this.GetTable<NewsFeed>();
    public ITable<NewsItem> NewsItems => this.GetTable<NewsItem>();
    public ITable<PostCandidate> PostCandidates => this.GetTable<PostCandidate>();
    public ITable<PostPublication> PostPublications => this.GetTable<PostPublication>();

    public DbContext(DataOptions dataOptions) : base(dataOptions) { }

    public static void AddRepositories(IServiceCollection services) {
        services.AddScoped<TokenRepository>();
        services.AddScoped<PermissionRepository>();
        services.AddScoped<UserChapterPermissionRepository>();
        services.AddScoped<UserGlobalPermissionRepository>();
        services.AddScoped<AdministrativeDivisionRepository>();
        services.AddScoped<UserRepository>();
        services.AddScoped<DueSelectionRepository>();
        services.AddScoped<ChapterRepository>();
        services.AddScoped<MembershipApplicationRepository>();
        services.AddScoped<ChapterOfficerRepository>();
        services.AddScoped<MotionRepository>();
        services.AddScoped<OptionRepository>();
        services.AddScoped<Templates.TemplateRepository>();
        services.AddScoped<MemberRepository>();
        services.AddScoped<EventRepository>();
        services.AddScoped<NotificationLogRepository>();
        services.AddScoped<UserNotificationPreferenceRepository>();
        services.AddScoped<TelegramLinkTokenRepository>();
        services.AddScoped<Submissions.PendingSubmissionRepository>();
        services.AddScoped<AuditLogRepository>();
        services.AddScoped<LoginAttemptRepository>();
        services.AddScoped<RoleRepository>();
        services.AddScoped<Meetings.MeetingRepository>();
        services.AddScoped<Meetings.AgendaItemRepository>();
        services.AddScoped<CollabDocumentRepository>();
        services.AddScoped<UsedSamlAssertionRepository>();
        services.AddScoped<ChapterMails.ChapterMailRepository>();
        services.AddScoped<Forms.ExpenseReimbursements.ExpenseReimbursementRepository>();
        services.AddScoped<Forms.PdfTemplates.PdfTemplateRepository>();
        services.AddScoped<ProgrammeRepository>();
        services.AddScoped<NewsFeedRepository>();
        services.AddScoped<NewsItemRepository>();
        services.AddScoped<PostPublicationRepository>();
    }

    /// <summary>Seeds reference data only; user accounts are the caller's responsibility.</summary>
    public static void SupplementDefaults(IServiceProvider services) {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<PermissionRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<RoleRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<TemplateRepository>().SupplementDefaults();
        NewsPostsSeeder.Seed(
            scope.ServiceProvider.GetRequiredService<NewsFeedRepository>(),
            scope.ServiceProvider.GetRequiredService<ProgrammeRepository>());
    }
}