using System;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Users;

public enum SsoLoginResult {
    Success,
    MemberExited,
    UserDeleted
}

public static class SsoLoginHelper {
    /// <summary>Returns the member carrying <see cref="SsoIdentity.MemberNumber"/>, creating a minimal one (names, email, number, voting rights)
    /// when the member import hasn't delivered that person yet. The next import fills the remaining fields by member number; until then
    /// the voting rights follow the account at every login.</summary>
    public static Member ResolveOrCreateMember(SsoIdentity identity, MemberRepository memberRepo) {
        var member = memberRepo.GetByMemberNumber(identity.MemberNumber);
        if (member != null) {
            FollowAccountVotingRights(member, identity.Eligible, memberRepo);
            return member;
        }

        member = new Member {
            MemberNumber = identity.MemberNumber,
            FirstName = identity.FirstName ?? "",
            LastName = identity.LastName ?? "",
            Email = identity.Email,
            HasVotingRights = identity.Eligible ?? false,
            LastImportedAt = DateTime.UtcNow
        };
        memberRepo.Insert(member);
        return member;
    }

    private static void FollowAccountVotingRights(Member member, bool? eligible, MemberRepository memberRepo) {
        if (member.IsImported)
            return;
        if (eligible == null || member.HasVotingRights == eligible.Value)
            return;

        memberRepo.SetVotingRights(member.Id, member.HasVotingRights, eligible.Value);
        member.HasVotingRights = eligible.Value;
    }

    /// <summary>The <c>ekklesia_eligible</c> value as Keycloak sends it ("true"/"false"); anything else is unknown.</summary>
    public static bool? ParseEligible(string? value) {
        return bool.TryParse(value, out var eligible) ? eligible : null;
    }

    public static (SsoLoginResult Result, Token? Token) ProcessSsoLogin(
        Member member,
        string email,
        string? issuedIp,
        string? issuedUserAgent,
        MemberRepository memberRepo,
        UserRepository userRepo,
        TokenRepository tokenRepo,
        ChapterOfficerRepository officerRepo) {

        var (result, user) = EnsureUser(member, email, memberRepo, userRepo, officerRepo);
        if (result != SsoLoginResult.Success)
            return (result, null);

        var token = tokenRepo.LoginUser(user!.Id, issuedIp, issuedUserAgent);
        return (SsoLoginResult.Success, token);
    }

    /// <summary>The user linked to the member, created and linked on the first call the same way a first login does.</summary>
    public static (SsoLoginResult Result, User? User) EnsureUser(
        Member member,
        string email,
        MemberRepository memberRepo,
        UserRepository userRepo,
        ChapterOfficerRepository officerRepo) {

        if (member.IsExited(DateTime.UtcNow))
            return (SsoLoginResult.MemberExited, null);

        var user = member.UserId.HasValue ? userRepo.Get(member.UserId.Value) : null;
        var isNewLink = false;

        if (user == null) {
            // Never adopt an existing user by email: the import syncs user emails from the CSV, so an email match
            // can belong to a different member than the one the SSO identity resolved to.
            user = new User {
                Email = email,
                Username = userRepo.IsUsernameTaken(email) ? null : email,
                FirstName = member.FirstName ?? "",
                LastName = member.LastName ?? ""
            };
            userRepo.Create(user);

            memberRepo.SetUserId(member.Id, user.Id);
            isNewLink = true;
        }

        if (user.DeletedAt.HasValue)
            return (SsoLoginResult.UserDeleted, null);

        // Grant officer permissions when a user is newly linked to a member
        if (isNewLink)
            officerRepo.GrantDefaultPermissionsForAllChapters(member.Id, user.Id);

        return (SsoLoginResult.Success, user);
    }
}
