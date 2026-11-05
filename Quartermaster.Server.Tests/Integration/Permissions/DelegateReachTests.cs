using System;
using System.Net;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Permissions;

/// <summary>
/// The lists that cover a chapter and the chapters below it: a state board member sees a Kreisverband's entries, a state delegate does not,
/// since the delegate role covers only the delegated chapter.
/// </summary>
public class DelegateReachTests : IntegrationTestBase {
    private Chapter _state = default!;
    private Chapter _district = default!;
    private string _boardMemberToken = "";
    private string _delegateToken = "";

    [Before(Test)]
    public void SeedChapters() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        _state = chain[1];
        _district = chain[2];
        _boardMemberToken = SeedHolderOf(PermissionIdentifier.SystemRole.ChapterOfficer);
        _delegateToken = SeedHolderOf(PermissionIdentifier.SystemRole.GeneralChapterDelegate);
    }

    private string SeedHolderOf(string roleIdentifier) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var role = new RoleRepository(Db).GetByIdentifier(roleIdentifier)!;
        Builder.AssignRoleToUser(user.Id, role.Id, _state.Id);
        return token;
    }

    private async Task<string> GetAs(string token, string url) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        return response.StatusCode == HttpStatusCode.OK ? body : $"{(int)response.StatusCode}";
    }

    private async Task AssertOnlyBoardMemberSees(string url, Guid entryId) {
        await Assert.That(await GetAs(_boardMemberToken, url)).Contains(entryId.ToString());
        await Assert.That(await GetAs(_delegateToken, url)).DoesNotContain(entryId.ToString());
    }

    [Test]
    public async Task Membership_applications() {
        var application = Builder.SeedMembershipApplication(chapterId: _district.Id);

        await AssertOnlyBoardMemberSees("/api/admin/membershipapplications?page=1&pageSize=50", application.Id);
    }

    [Test]
    public async Task Due_selections() {
        var due = Builder.SeedDueSelection();
        Db.DueSelections.Where(d => d.Id == due.Id).Set(d => d.ChapterId, _district.Id).Update();

        await AssertOnlyBoardMemberSees("/api/admin/dueselections?page=1&pageSize=50", due.Id);
    }

    [Test]
    public async Task Members() {
        var member = Builder.SeedMember(chapterId: _district.Id, memberNumber: 4711);

        await AssertOnlyBoardMemberSees("/api/members?page=1&pageSize=50", member.Id);
    }

    [Test]
    public async Task Dashboard_applications() {
        var application = Builder.SeedMembershipApplication(chapterId: _district.Id);

        await AssertOnlyBoardMemberSees("/api/dashboard", application.Id);
    }

    [Test]
    public async Task Chapter_mails() {
        var mail = new ChapterMail {
            ChapterId = _district.Id,
            Title = "Einladung",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = _district.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = Builder.SeedUser().Id
        };
        Db.Insert(mail);

        await AssertOnlyBoardMemberSees("/api/chapter-mail", mail.Id);
    }

    [Test]
    public async Task Signature_campaigns() {
        var campaign = new SignatureCampaign {
            ChapterId = _district.Id,
            Name = "Kreistagswahl",
            Target = 100,
            Deadline = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(campaign);

        await AssertOnlyBoardMemberSees("/api/support-signatures/campaigns", campaign.Id);
    }
}
