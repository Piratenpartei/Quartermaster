using System;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Users;

public enum SsoLoginResult {
    Success,
    NoMember,
    MemberExited,
    UserDeleted
}

public static class SsoLoginHelper {
    public static (SsoLoginResult Result, string? TokenContent) ProcessSsoLogin(
        string email,
        MemberRepository memberRepo,
        UserRepository userRepo,
        TokenRepository tokenRepo,
        ChapterOfficerRepository officerRepo) {

        var member = memberRepo.GetByEmail(email);
        if (member == null)
            return (SsoLoginResult.NoMember, null);

        if (member.ExitDate.HasValue)
            return (SsoLoginResult.MemberExited, null);

        var user = member.UserId.HasValue ? userRepo.GetById(member.UserId.Value) : null;
        var isNewLink = false;

        if (user == null) {
            user = userRepo.GetByEmail(email);

            if (user == null) {
                user = new User {
                    EMail = email,
                    Username = email,
                    FirstName = member.FirstName ?? "",
                    LastName = member.LastName ?? ""
                };
                userRepo.Create(user);
            }

            memberRepo.SetUserId(member.Id, user.Id);
            isNewLink = true;
        }

        if (user.DeletedAt.HasValue)
            return (SsoLoginResult.UserDeleted, null);

        // Grant officer permissions when a user is newly linked to a member
        if (isNewLink)
            officerRepo.GrantDefaultPermissionsForAllChapters(member.Id, user.Id);

        var token = tokenRepo.LoginUser(user.Id);
        return (SsoLoginResult.Success, token.Content);
    }
}
