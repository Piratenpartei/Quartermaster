using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Api.Notifications;
using Quartermaster.Api.Permissions;
using Quartermaster.Api.Roles;
using Quartermaster.Api.Templates;
using Quartermaster.Data;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Roles;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ApplicantRegister;

public class ApplicantRegisterVisibilityTests : IntegrationTestBase {
    private const string TriggerId = "applicant_register_hit";
    private const string EmailTemplateIdentifier = "notifications.applicant_register_hit.email";

    public ApplicantRegisterVisibilityTests() {
        DbContext.SupplementDefaults(Factory.Services);
    }

    [Test]
    public async Task Trigger_hidden_without_permission() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser();
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });

        using var plainClient = AuthenticatedClient(plainToken);
        var plain = await plainClient.GetFromJsonAsync<NotificationPreferencesDTO>("/api/users/notification-preferences");
        using var holderClient = AuthenticatedClient(holderToken);
        var holder = await holderClient.GetFromJsonAsync<NotificationPreferencesDTO>("/api/users/notification-preferences");

        await Assert.That(plain!.Triggers.Any(t => t.TriggerId == TriggerId)).IsFalse();
        await Assert.That(plain.Cells.Any(c => c.TriggerId == TriggerId)).IsFalse();
        await Assert.That(holder!.Triggers.Any(t => t.TriggerId == TriggerId)).IsTrue();
        await Assert.That(holder.Cells.Any(c => c.TriggerId == TriggerId)).IsTrue();
    }

    [Test]
    public async Task Update_for_hidden_trigger_is_ignored_like_an_unknown_trigger() {
        var (plainUser, plainToken) = Builder.SeedAuthenticatedUser();
        var (holderUser, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        var request = new UpdateNotificationPreferencesRequest {
            Cells = new() { new() { TriggerId = TriggerId, ChannelId = "email", Enabled = false } }
        };

        using var plainClient = await AuthenticatedClientWithCsrfAsync(plainToken);
        await plainClient.PutAsJsonAsync("/api/users/notification-preferences", request);
        using var holderClient = await AuthenticatedClientWithCsrfAsync(holderToken);
        await holderClient.PutAsJsonAsync("/api/users/notification-preferences", request);

        await Assert.That(Db.UserNotificationPreferences.Count(p => p.UserId == plainUser.Id)).IsEqualTo(0);
        await Assert.That(Db.UserNotificationPreferences.Count(p => p.UserId == holderUser.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Stats_catalog_hides_trigger_and_templates_without_permission() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs, PermissionIdentifier.ManageApplicantRegister });

        using var plainClient = AuthenticatedClient(plainToken);
        var plain = await plainClient.GetFromJsonAsync<NotificationStatsCatalogDTO>("/api/notifications/stats/catalog");
        using var holderClient = AuthenticatedClient(holderToken);
        var holder = await holderClient.GetFromJsonAsync<NotificationStatsCatalogDTO>("/api/notifications/stats/catalog");

        await Assert.That(plain!.Triggers.Any(t => t.Value == TriggerId)).IsFalse();
        await Assert.That(plain.Templates.Any(t => t.Value.Contains("applicant_register_hit"))).IsFalse();
        await Assert.That(holder!.Triggers.Any(t => t.Value == TriggerId)).IsTrue();
        await Assert.That(holder.Templates.Any(t => t.Value == EmailTemplateIdentifier)).IsTrue();
    }

    [Test]
    public async Task Stats_hides_register_log_rows_without_permission() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs, PermissionIdentifier.ManageApplicantRegister });
        Db.Insert(new NotificationLog {
            ChannelId = "email",
            Recipient = "x@test.local",
            TriggerId = TriggerId,
            TemplateIdentifier = EmailTemplateIdentifier,
            Status = "Sent",
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        });

        using var plainClient = AuthenticatedClient(plainToken);
        var plain = await plainClient.GetFromJsonAsync<NotificationStatsDTO>("/api/notifications/stats");
        using var holderClient = AuthenticatedClient(holderToken);
        var holder = await holderClient.GetFromJsonAsync<NotificationStatsDTO>("/api/notifications/stats");

        await Assert.That(plain!.Sent).IsEqualTo(0);
        await Assert.That(holder!.Sent).IsEqualTo(1);
    }

    [Test]
    public async Task Notification_log_hides_register_rows_without_permission() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs, PermissionIdentifier.ManageApplicantRegister });
        Db.Insert(new NotificationLog {
            ChannelId = "email",
            Recipient = "x@test.local",
            TriggerId = TriggerId,
            TemplateIdentifier = EmailTemplateIdentifier,
            Status = "Sent",
            CreatedAt = DateTime.UtcNow
        });

        using var plainClient = AuthenticatedClient(plainToken);
        var plain = await plainClient.GetFromJsonAsync<List<NotificationLogDTO>>("/api/notificationlogs");
        using var holderClient = AuthenticatedClient(holderToken);
        var holder = await holderClient.GetFromJsonAsync<List<NotificationLogDTO>>("/api/notificationlogs");

        await Assert.That(plain!.Count).IsEqualTo(0);
        await Assert.That(holder!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Notification_log_page_is_filled_with_visible_rows() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs, PermissionIdentifier.ManageApplicantRegister });
        var start = DateTime.UtcNow.AddHours(-2);
        for (var i = 0; i < 60; i++) {
            var hidden = i % 2 == 0;
            Db.Insert(new NotificationLog {
                ChannelId = "email",
                Recipient = "x@test.local",
                TriggerId = hidden ? TriggerId : "motion_submitted",
                TemplateIdentifier = hidden ? EmailTemplateIdentifier : "notifications.motion_submitted.email",
                Status = "Sent",
                CreatedAt = start.AddMinutes(i)
            });
        }

        using var plainClient = AuthenticatedClient(plainToken);
        var plain = await plainClient.GetFromJsonAsync<List<NotificationLogDTO>>("/api/notificationlogs");
        using var holderClient = AuthenticatedClient(holderToken);
        var holder = await holderClient.GetFromJsonAsync<List<NotificationLogDTO>>("/api/notificationlogs");

        await Assert.That(plain!.Count).IsEqualTo(30);
        await Assert.That(plain.Any(l => l.TriggerId == TriggerId)).IsFalse();
        await Assert.That(holder!.Count).IsEqualTo(50);
    }

    [Test]
    public async Task Notification_log_page_of_fifty_returns_fifty_visible_rows() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        var start = DateTime.UtcNow.AddHours(-3);
        for (var i = 0; i < 120; i++) {
            var hidden = i >= 80;
            Db.Insert(new NotificationLog {
                ChannelId = "email",
                Recipient = "x@test.local",
                TriggerId = hidden ? TriggerId : "motion_submitted",
                TemplateIdentifier = hidden ? EmailTemplateIdentifier : "notifications.motion_submitted.email",
                Status = "Sent",
                CreatedAt = start.AddMinutes(i)
            });
        }

        using var plainClient = AuthenticatedClient(plainToken);
        var plain = await plainClient.GetFromJsonAsync<List<NotificationLogDTO>>("/api/notificationlogs");

        await Assert.That(plain!.Count).IsEqualTo(50);
        await Assert.That(plain.Any(l => l.TriggerId == TriggerId)).IsFalse();
    }

    [Test]
    public async Task Chapter_mail_hides_restricted_template() {
        var chapter = Builder.SeedChapter("C");
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ManageChapterMail } });
        var restricted = Db.Templates.First(t => t.Identifier == EmailTemplateIdentifier && t.ChapterId == null);
        var visible = Db.Templates.First(t => t.Identifier == "notifications.motion_submitted.email" && t.ChapterId == null);
        var mail = new ChapterMail {
            ChapterId = chapter.Id,
            Title = "Mail",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            TemplateId = restricted.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        Db.Insert(mail);
        var body = new ChapterMailCreateRequest {
            ChapterId = chapter.Id,
            Title = "Neu",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            IncludeDescendants = false,
            TemplateId = restricted.Id
        };

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var detail = await client.GetAsync($"/api/chapter-mail/{mail.Id}");
        var create = await client.PostAsJsonAsync("/api/chapter-mail", body);
        body.TemplateId = visible.Id;
        var createVisible = await client.PostAsJsonAsync("/api/chapter-mail", body);
        var update = await client.PutAsJsonAsync($"/api/chapter-mail/{mail.Id}", new {
            Title = "Neu",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            IncludeDescendants = false,
            TemplateId = restricted.Id
        });

        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(createVisible.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ChapterMails.Count(m => m.TemplateId == restricted.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Chapter_mail_treats_unknown_template_like_a_hidden_one() {
        var chapter = Builder.SeedChapter("C");
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ManageChapterMail } });
        var mail = new ChapterMail {
            ChapterId = chapter.Id,
            Title = "Mail",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        Db.Insert(mail);
        var unknownTemplateId = Guid.NewGuid();

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var create = await client.PostAsJsonAsync("/api/chapter-mail", new ChapterMailCreateRequest {
            ChapterId = chapter.Id,
            Title = "Neu",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            IncludeDescendants = false,
            TemplateId = unknownTemplateId
        });
        var update = await client.PutAsJsonAsync($"/api/chapter-mail/{mail.Id}", new {
            Title = "Neu",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            IncludeDescendants = false,
            TemplateId = unknownTemplateId
        });

        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(Db.ChapterMails.Count(m => m.TemplateId == unknownTemplateId)).IsEqualTo(0);
    }

    [Test]
    public async Task Chapter_mail_draft_with_deleted_template_stays_editable() {
        var chapter = Builder.SeedChapter("C");
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ManageChapterMail } });
        var deleted = new Template {
            DisplayName = "Alt",
            ChapterId = chapter.Id,
            Body = "Hallo",
            CreatedAt = DateTime.UtcNow,
            DeletedAt = DateTime.UtcNow
        };
        Db.Insert(deleted);
        var mail = new ChapterMail {
            ChapterId = chapter.Id,
            Title = "Mail",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            TemplateId = deleted.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        Db.Insert(mail);

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var update = await client.PutAsJsonAsync($"/api/chapter-mail/{mail.Id}", new {
            Title = "Neu",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            IncludeDescendants = false,
            TemplateId = deleted.Id
        });

        await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ChapterMails.Single(m => m.Id == mail.Id).Title).IsEqualTo("Neu");
    }

    [Test]
    public async Task Chapter_mail_send_hides_restricted_template() {
        var chapter = Builder.SeedChapter("C");
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ManageChapterMail } });
        var restricted = Db.Templates.First(t => t.Identifier == EmailTemplateIdentifier && t.ChapterId == null);
        var mail = new ChapterMail {
            ChapterId = chapter.Id,
            Title = "Mail",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            TemplateId = restricted.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        Db.Insert(mail);

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var send = await client.PostAsJsonAsync($"/api/chapter-mail/{mail.Id}/send", new { });

        await Assert.That(send.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(Db.ChapterMails.Single(m => m.Id == mail.Id).IsSent).IsFalse();
    }

    [Test]
    public async Task Chapter_mail_allows_restricted_template_for_register_managers() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister },
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ManageChapterMail } });
        var restricted = Db.Templates.First(t => t.Identifier == EmailTemplateIdentifier && t.ChapterId == null);

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var create = await client.PostAsJsonAsync("/api/chapter-mail", new ChapterMailCreateRequest {
            ChapterId = chapter.Id,
            Title = "Neu",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            IncludeDescendants = false,
            TemplateId = restricted.Id
        });

        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Templates_hidden_without_permission() {
        var chapter = Builder.SeedChapter("C");
        var (_, viewerToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewTemplates } });
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewTemplates, PermissionIdentifier.ManageApplicantRegister });
        var template = Db.Templates.First(t => t.Identifier == EmailTemplateIdentifier && t.ChapterId == null);

        using var viewerClient = AuthenticatedClient(viewerToken);
        var viewerList = await viewerClient.GetFromJsonAsync<List<TemplateListItemDTO>>("/api/templates");
        var viewerDirect = await viewerClient.GetAsync($"/api/templates/{template.Id}");
        var viewerPdf = await viewerClient.GetAsync($"/api/templates/{template.Id}/pdf");

        using var holderClient = AuthenticatedClient(holderToken);
        var holderList = await holderClient.GetFromJsonAsync<List<TemplateListItemDTO>>("/api/templates");
        var holderDirect = await holderClient.GetAsync($"/api/templates/{template.Id}");

        await Assert.That(viewerList!.Any(t => (t.Identifier ?? "").Contains("applicant_register_hit"))).IsFalse();
        await Assert.That(viewerList.Count).IsGreaterThan(0);
        await Assert.That(viewerDirect.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(viewerPdf.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(holderList!.Count(t => (t.Identifier ?? "").Contains("applicant_register_hit"))).IsEqualTo(2);
        await Assert.That(holderDirect.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Template_writes_hidden_without_permission() {
        var chapter = Builder.SeedChapter("C");
        var (_, editorToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewTemplates, PermissionIdentifier.EditTemplates },
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditTemplates } });
        var template = Db.Templates.First(t => t.Identifier == EmailTemplateIdentifier && t.ChapterId == null);

        using var client = await AuthenticatedClientWithCsrfAsync(editorToken);
        var update = await client.PutAsJsonAsync($"/api/templates/{template.Id}", new TemplateUpdateRequest {
            Id = template.Id,
            DisplayName = "x",
            Body = "x"
        });
        var overrideResponse = await client.PostAsJsonAsync($"/api/templates/{template.Id}/overrides", new TemplateOverrideUpsertRequest {
            TemplateId = template.Id,
            ChapterId = chapter.Id,
            Body = "x"
        });
        var delete = await client.DeleteAsync($"/api/templates/{template.Id}");

        await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(overrideResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(Db.Templates.First(t => t.Id == template.Id).Body).IsNotEqualTo("x");
    }

    [Test]
    public async Task Permission_not_in_chapter_lists() {
        var (_, adminToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers, PermissionIdentifier.ManageRoles });

        using var client = await AuthenticatedClientWithCsrfAsync(adminToken);
        var permissions = await client.GetFromJsonAsync<List<PermissionDTO>>("/api/permissions");
        var chapterRole = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "Officer",
            Scope = RoleScope.ChapterScoped,
            Permissions = new() { PermissionIdentifier.ManageApplicantRegister }
        });

        var register = permissions!.Single(p => p.Identifier == PermissionIdentifier.ManageApplicantRegister);
        await Assert.That(register.Global).IsTrue();
        await Assert.That(chapterRole.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(PermissionIdentifier.DefaultOfficerPermissions.Contains(PermissionIdentifier.ManageApplicantRegister)).IsFalse();
        await Assert.That(PermissionIdentifier.BoardWorkPermissions.Contains(PermissionIdentifier.ManageApplicantRegister)).IsFalse();
    }

    [Test]
    public async Task Hit_notification_reaches_only_holders() {
        var root = Builder.SeedChapter("Root");
        var (holder, _) = Builder.SeedAuthenticatedUser(
            email: "holder@test.local",
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        Builder.SeedAuthenticatedUser(
            email: "officer@test.local",
            chapterPermissions: new() { [root.Id] = new[] { PermissionIdentifier.ProcessApplications, PermissionIdentifier.ViewApplications } });

        var payload = new ApplicantRegisterHitPayload(root.Id, "/Administration/MembershipApplications/abc", 2);
        using var scope = Factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<INotificationDispatchQueue>();
        queue.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.ApplicantRegisterHit,
            payload,
            _ => new Dictionary<string, object> { ["hit"] = payload },
            "MembershipApplication",
            Guid.NewGuid()));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == TriggerId).ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(holder.Id);
        await Assert.That(logs[0].Body!.Contains("2 Einträgen")).IsTrue();
        await Assert.That(logs[0].Body!.Contains("/Administration/MembershipApplications/abc")).IsTrue();
    }

    [Test]
    public async Task Hit_notification_ignores_chapter_grants_and_reaches_role_holders() {
        var root = Builder.SeedChapter("Root");
        var (holder, _) = Builder.SeedAuthenticatedUser(
            email: "holder@test.local",
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        Builder.SeedAuthenticatedUser(
            email: "chaptergrant@test.local",
            chapterPermissions: new() { [root.Id] = new[] { PermissionIdentifier.ManageApplicantRegister } });
        var (roleHolder, _) = Builder.SeedAuthenticatedUser(email: "roleholder@test.local");
        var role = Builder.SeedRole("register_role", "Register", RoleScope.Global);
        Db.Insert(new RolePermission { RoleId = role.Id, PermissionIdentifier = PermissionIdentifier.ManageApplicantRegister });
        Builder.AssignRoleToUser(roleHolder.Id, role.Id);
        Builder.SeedAuthenticatedUser(
            email: "officer@test.local",
            chapterPermissions: new() { [root.Id] = new[] { PermissionIdentifier.ProcessApplications, PermissionIdentifier.ViewApplications } });

        var payload = new ApplicantRegisterHitPayload(root.Id, "/Administration/MembershipApplications/abc", 2);
        using var scope = Factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<INotificationDispatchQueue>();
        queue.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.ApplicantRegisterHit,
            payload,
            _ => new Dictionary<string, object> { ["hit"] = payload },
            "MembershipApplication",
            Guid.NewGuid()));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == TriggerId).ToList();
        await Assert.That(logs.Count).IsEqualTo(2);
        await Assert.That(logs.Select(l => l.RecipientUserId).Contains(holder.Id)).IsTrue();
        await Assert.That(logs.Select(l => l.RecipientUserId).Contains(roleHolder.Id)).IsTrue();
    }
}
