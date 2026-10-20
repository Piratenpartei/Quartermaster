using System;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Accreditation.Endpoints;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Users;

public record KeycloakUserImportResult(Guid? UserId, string? Problem);

/// <summary>
/// Creates the user for a Keycloak account before its first login, the way that login would: the member is found or created
/// by member number, the user linked to it, officer permissions granted, and the department mapped to a chapter.
/// </summary>
public class KeycloakUserImporter {
    private readonly MemberRepository _members;
    private readonly UserRepository _users;
    private readonly ChapterOfficerRepository _officers;
    private readonly KeycloakDepartmentChapterAssigner _departments;
    private readonly AuditLogRepository _audit;

    public KeycloakUserImporter(MemberRepository members, UserRepository users, ChapterOfficerRepository officers,
        KeycloakDepartmentChapterAssigner departments, AuditLogRepository audit) {
        _members = members;
        _users = users;
        _officers = officers;
        _departments = departments;
        _audit = audit;
    }

    public KeycloakUserCandidateDTO Describe(KeycloakUserRepresentation account) {
        var member = MemberOf(account);
        var existing = member?.UserId == null ? null : _users.Get(member.UserId.Value);
        return new KeycloakUserCandidateDTO {
            Account = KeycloakAccountMapper.ToDto(account),
            ExistingUserId = existing?.DeletedAt == null ? existing?.Id : null,
            ChapterName = _departments.ChapterFor(account.Attribute(KeycloakUserRepresentation.DepartmentAttribute))?.Name,
            Problem = Problem(account, member, existing)
        };
    }

    public KeycloakUserImportResult Import(KeycloakUserRepresentation account) {
        var member = MemberOf(account);
        var existing = member?.UserId == null ? null : _users.Get(member.UserId.Value);
        var problem = Problem(account, member, existing);
        if (problem != null)
            return new KeycloakUserImportResult(null, problem);
        if (existing != null)
            return new KeycloakUserImportResult(null, I18nKey.Error.KeycloakAccount.UserExists);

        MemberNumberParser.TryParse(account.Attribute("member_number"), out var memberNumber);
        var department = account.Attribute(KeycloakUserRepresentation.DepartmentAttribute);
        var eligible = SsoLoginHelper.ParseEligible(account.Attribute(KeycloakUserRepresentation.EligibleAttribute));
        var identity = new SsoIdentity(account.Email!.Trim(), memberNumber, account.FirstName, account.LastName, department, eligible);
        member = SsoLoginHelper.ResolveOrCreateMember(identity, _members);

        var (result, user) = SsoLoginHelper.EnsureUser(member, identity.Email, _members, _users, _officers);
        if (result != SsoLoginResult.Success)
            return new KeycloakUserImportResult(null, ProblemFor(result));

        _audit.LogCreated("User", user!.Id);
        _departments.Assign(member, department);
        return new KeycloakUserImportResult(user.Id, null);
    }

    private Member? MemberOf(KeycloakUserRepresentation account) {
        return MemberNumberParser.TryParse(account.Attribute("member_number"), out var memberNumber)
            ? _members.GetByMemberNumber(memberNumber)
            : null;
    }

    private static string? Problem(KeycloakUserRepresentation account, Member? member, User? existing) {
        if (!MemberNumberParser.TryParse(account.Attribute("member_number"), out _))
            return I18nKey.Error.KeycloakAccount.NoMemberNumber;
        if (account.Attribute("closed_time") != null)
            return I18nKey.Error.KeycloakAccount.Closed;
        if (string.IsNullOrWhiteSpace(account.Email))
            return I18nKey.Error.KeycloakAccount.NoEmail;
        if (member != null && member.IsExited(DateTime.UtcNow))
            return I18nKey.Error.KeycloakAccount.MemberExited;
        if (existing?.DeletedAt != null)
            return I18nKey.Error.KeycloakAccount.UserDeleted;
        return null;
    }

    private static string ProblemFor(SsoLoginResult result) {
        return result == SsoLoginResult.MemberExited
            ? I18nKey.Error.KeycloakAccount.MemberExited
            : I18nKey.Error.KeycloakAccount.UserDeleted;
    }
}
