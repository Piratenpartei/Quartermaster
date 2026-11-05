using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Tests.Accreditation.Fakes;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Users;

namespace Quartermaster.Server.Tests.Users;

[NotInParallel("FakeKeycloakAdminClient")]
public class KeycloakUserRefresherTests : RepositoryTestBase {
    private readonly DateTime _now = DateTime.UtcNow;
    private TestDataBuilder _builder = default!;
    private KeycloakUserRefresher _refresher = default!;

    [Before(Test)]
    public void Setup() {
        FakeKeycloakAdminClient.Reset();
        _builder = new TestDataBuilder(Db);
        var members = new MemberRepository(Db, AuditLog);
        var users = new UserRepository(Db, new UserGlobalPermissionRepository(Db, new RoleRepository(Db)), new PermissionRepository(Db));
        var departments = new KeycloakDepartmentChapterAssigner(new ChapterRepository(Db, AuditLog), members);
        _refresher = new KeycloakUserRefresher(new FakeKeycloakAdminClient(), members, users, departments, NullLogger<KeycloakUserRefresher>.Instance);
    }

    private (Member Member, User User) SeedLinkedMember(Guid? chapterId = null, DateTime? exitDate = null) {
        var user = _builder.SeedUser();
        var member = _builder.SeedMember(chapterId: chapterId, exitDate: exitDate, userId: user.Id);
        return (member, user);
    }

    private User UserOf(Guid userId) => Db.Users.Single(u => u.Id == userId);

    private Member MemberOf(Guid memberId) => Db.Members.Single(m => m.Id == memberId);

    [Test]
    public async Task Stores_the_keycloak_id_of_a_linked_user() {
        var (member, user) = SeedLinkedMember();
        var id = Guid.NewGuid().ToString();
        FakeKeycloakAdminClient.AddUser(id, "anna.pirat", member.MemberNumber.ToString());

        var first = await _refresher.RefreshAsync(_now, CancellationToken.None);
        var second = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(UserOf(user.Id).KeycloakUserId).IsEqualTo(id);
        await Assert.That(first.AccountsRead).IsEqualTo(1);
        await Assert.That(first.KeycloakIdsStored).IsEqualTo(1);
        await Assert.That(second.KeycloakIdsStored).IsEqualTo(0);
    }

    [Test]
    public async Task Sets_the_keycloak_username_of_a_linked_user() {
        var (member, user) = SeedLinkedMember();
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", member.MemberNumber.ToString());

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(UserOf(user.Id).Username).IsEqualTo("anna.pirat");
        await Assert.That(result.UsernamesSet).IsEqualTo(1);
        await Assert.That(result.UsernamesSkipped).IsEqualTo(0);
    }

    /// <summary>A rename in Keycloak can hand an account's old name to an account listed before it; that one gets it in the second pass.</summary>
    [Test]
    public async Task A_name_freed_later_in_the_same_run_is_taken_in_the_second_pass() {
        var (firstMember, firstUser) = SeedLinkedMember();
        var (secondMember, secondUser) = SeedLinkedMember();
        Db.Users.Where(u => u.Id == secondUser.Id).Set(u => u.Username, "anna.pirat").Update();
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", firstMember.MemberNumber.ToString());
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat2", secondMember.MemberNumber.ToString());

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(UserOf(firstUser.Id).Username).IsEqualTo("anna.pirat");
        await Assert.That(UserOf(secondUser.Id).Username).IsEqualTo("anna.pirat2");
        await Assert.That(result.UsernamesSet).IsEqualTo(2);
        await Assert.That(result.UsernamesSkipped).IsEqualTo(0);
    }

    [Test]
    public async Task A_name_another_account_keeps_is_counted_as_skipped() {
        var (member, user) = SeedLinkedMember();
        _builder.SeedUser(username: "anna.pirat");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", member.MemberNumber.ToString());

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(UserOf(user.Id).Username).IsNotEqualTo("anna.pirat");
        await Assert.That(result.UsernamesSet).IsEqualTo(0);
        await Assert.That(result.UsernamesSkipped).IsEqualTo(1);
    }

    [Test]
    public async Task Assigns_the_department_chapter_only_to_members_without_one() {
        var department = $"Piratenpartei {Guid.NewGuid():N}";
        var state = _builder.SeedChapter("LV", keycloakDepartment: department);
        var county = _builder.SeedChapter("KV", parentChapterId: state.Id);
        var otherState = _builder.SeedChapter("Other LV");
        var (pending, _) = SeedLinkedMember();
        var (placed, _) = SeedLinkedMember(chapterId: county.Id);
        var (elsewhere, _) = SeedLinkedMember(chapterId: otherState.Id);
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", pending.MemberNumber.ToString(), department: department);
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "bernd.pirat", placed.MemberNumber.ToString(), department: department);
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "carla.pirat", elsewhere.MemberNumber.ToString(), department: department);

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(result.ChaptersAssigned).IsEqualTo(1);
        await Assert.That(MemberOf(pending.Id).ChapterId).IsEqualTo(state.Id);
        await Assert.That(MemberOf(placed.Id).ChapterId).IsEqualTo(county.Id);
        await Assert.That(MemberOf(elsewhere.Id).ChapterId).IsEqualTo(otherState.Id);
    }

    [Test]
    public async Task Voting_rights_follow_the_account_only_until_the_import_delivered_the_member() {
        var (pending, _) = SeedLinkedMember();
        var (imported, _) = SeedLinkedMember();
        Db.Members
            .Where(m => m.Id == imported.Id)
            .Set(m => m.IsImported, true)
            .Update();
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", pending.MemberNumber.ToString(), eligible: "true");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "bernd.pirat", imported.MemberNumber.ToString(), eligible: "true");

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(result.VotingRightsChanged).IsEqualTo(1);
        await Assert.That(MemberOf(pending.Id).HasVotingRights).IsTrue();
        await Assert.That(MemberOf(imported.Id).HasVotingRights).IsFalse();
    }

    [Test]
    public async Task Accounts_without_a_user_are_counted_and_nothing_is_created() {
        var unlinked = _builder.SeedMember();
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", unlinked.MemberNumber.ToString());
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "bernd.pirat", "987654321");
        var usersBefore = Db.Users.Count();

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(result.AccountsWithoutUser).IsEqualTo(2);
        await Assert.That(Db.Users.Count()).IsEqualTo(usersBefore);
        await Assert.That(MemberOf(unlinked.Id).UserId).IsNull();
    }

    [Test]
    public async Task Closed_accounts_exited_members_and_accounts_without_member_number_are_skipped() {
        var (closed, closedUser) = SeedLinkedMember();
        var (exited, exitedUser) = SeedLinkedMember(exitDate: _now.AddDays(-1));
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", closed.MemberNumber.ToString(), closed: true);
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "bernd.pirat", exited.MemberNumber.ToString());
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "service-account", "");

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(result.AccountsRead).IsEqualTo(3);
        await Assert.That(result.KeycloakIdsStored).IsEqualTo(0);
        await Assert.That(result.AccountsWithoutUser).IsEqualTo(0);
        await Assert.That(UserOf(closedUser.Id).KeycloakUserId).IsNull();
        await Assert.That(UserOf(exitedUser.Id).KeycloakUserId).IsNull();
    }

    [Test]
    public async Task Reads_every_page() {
        for (var i = 0; i < KeycloakUserRefresher.PageSize; i++)
            FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), $"filler{i}", "");
        var (member, user) = SeedLinkedMember();
        var id = Guid.NewGuid().ToString();
        FakeKeycloakAdminClient.AddUser(id, "anna.pirat", member.MemberNumber.ToString());

        var result = await _refresher.RefreshAsync(_now, CancellationToken.None);

        await Assert.That(result.AccountsRead).IsEqualTo(KeycloakUserRefresher.PageSize + 1);
        await Assert.That(UserOf(user.Id).KeycloakUserId).IsEqualTo(id);
    }

    [Test]
    public async Task Keycloak_failure_surfaces_as_keycloak_exception() {
        FakeKeycloakAdminClient.FailNext = new KeycloakException(500, "down");

        await Assert.That(() => _refresher.RefreshAsync(_now, CancellationToken.None)).Throws<KeycloakException>();
    }
}
