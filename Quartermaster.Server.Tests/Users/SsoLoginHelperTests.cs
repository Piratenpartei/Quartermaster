using LinqToDB;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Users;

namespace Quartermaster.Server.Tests.Users;

public class SsoLoginHelperTests : RepositoryTestBase {
    private MemberRepository _memberRepo = default!;
    private UserRepository _userRepo = default!;
    private TokenRepository _tokenRepo = default!;
    private ChapterOfficerRepository _officerRepo = default!;

    [Before(Test)]
    public void Setup() {
        var roleRepo = new RoleRepository(Db);
        var permissionRepo = new PermissionRepository(Db);
        var optionRepo = new OptionRepository(Db, AuditLog);
        _memberRepo = new MemberRepository(Db, AuditLog);
        _userRepo = new UserRepository(Db, new UserGlobalPermissionRepository(Db, roleRepo), permissionRepo);
        _tokenRepo = new TokenRepository(Db, optionRepo, new ChapterRepository(Db, AuditLog));
        _officerRepo = new ChapterOfficerRepository(Db, AuditLog, roleRepo);

        Db.Insert(new AdministrativeDivision { Id = Guid.Empty, Name = "Default Division", Depth = 0 });
        optionRepo.SupplementDefaults();
    }

    private (SsoLoginResult Result, Token? Token) Login(SsoIdentity identity) {
        var member = SsoLoginHelper.ResolveOrCreateMember(identity, _memberRepo);
        return SsoLoginHelper.ProcessSsoLogin(member, identity.Email, null, null,
            _memberRepo, _userRepo, _tokenRepo, _officerRepo);
    }

    private Member InsertMember(int memberNumber, string email) {
        var member = new Member {
            MemberNumber = memberNumber,
            FirstName = "Imported",
            LastName = "Member",
            Email = email,
            Street = "Hauptstraße 1",
            LastImportedAt = DateTime.UtcNow
        };
        _memberRepo.Insert(member);
        return member;
    }

    [Test]
    public async Task Unknown_member_number_creates_minimal_member_and_linked_user() {
        var (result, token) = Login(new SsoIdentity("new@example.org", 4711, "Erika", "Mustermann"));

        await Assert.That(result).IsEqualTo(SsoLoginResult.Success);
        await Assert.That(token).IsNotNull();

        var member = _memberRepo.GetByMemberNumber(4711)!;
        await Assert.That(member.FirstName).IsEqualTo("Erika");
        await Assert.That(member.LastName).IsEqualTo("Mustermann");
        await Assert.That(member.Email).IsEqualTo("new@example.org");
        await Assert.That(member.ChapterId).IsNull();

        var user = _userRepo.Get(member.UserId!.Value)!;
        await Assert.That(user.Id).IsEqualTo(token!.UserId);
        await Assert.That(user.Username).IsEqualTo("new@example.org");
    }

    [Test]
    public async Task Existing_member_is_matched_by_number_even_when_email_differs() {
        var existing = InsertMember(1234, "csv@example.org");

        var (result, _) = Login(new SsoIdentity("sso@example.org", 1234, "Other", "Name"));

        await Assert.That(result).IsEqualTo(SsoLoginResult.Success);
        await Assert.That(Db.Members.Count()).IsEqualTo(1);

        var member = _memberRepo.Get(existing.Id)!;
        await Assert.That(member.FirstName).IsEqualTo("Imported");
        await Assert.That(member.Street).IsEqualTo("Hauptstraße 1");
        await Assert.That(_userRepo.Get(member.UserId!.Value)!.Email).IsEqualTo("sso@example.org");
    }

    [Test]
    public async Task Second_login_reuses_the_linked_user() {
        Login(new SsoIdentity("a@example.org", 1, "A", "B"));
        Login(new SsoIdentity("a@example.org", 1, "A", "B"));

        await Assert.That(Db.Users.Count()).IsEqualTo(1);
        await Assert.That(Db.Members.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Login_never_adopts_a_user_of_another_member_with_the_same_email() {
        var otherMember = InsertMember(100, "shared@example.org");
        var otherUser = new User { Email = "shared@example.org", Username = "shared@example.org" };
        _userRepo.Create(otherUser);
        _memberRepo.SetUserId(otherMember.Id, otherUser.Id);

        var (result, token) = Login(new SsoIdentity("shared@example.org", 200, "Else", "Where"));

        await Assert.That(result).IsEqualTo(SsoLoginResult.Success);
        await Assert.That(token!.UserId).IsNotEqualTo(otherUser.Id);

        var newUser = _userRepo.Get(token.UserId!.Value)!;
        await Assert.That(newUser.Username).IsNull();
        await Assert.That(_memberRepo.GetByMemberNumber(200)!.UserId).IsEqualTo(newUser.Id);
    }

    [Test]
    public async Task Exited_member_is_rejected() {
        var member = InsertMember(300, "gone@example.org");
        Db.Members
            .Where(m => m.Id == member.Id)
            .Set(m => m.ExitDate, DateTime.UtcNow.AddDays(-1))
            .Update();

        var (result, token) = Login(new SsoIdentity("gone@example.org", 300, null, null));

        await Assert.That(result).IsEqualTo(SsoLoginResult.MemberExited);
        await Assert.That(token).IsNull();
    }
}
