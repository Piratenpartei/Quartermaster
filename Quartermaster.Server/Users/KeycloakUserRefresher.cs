using System;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.Users;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Users;

/// <summary>
/// Applies every Keycloak account to its existing user the way a login would, without the session: stores the Keycloak id, lets the voting rights
/// of members the import hasn't delivered yet follow the account, and maps the department to a chapter. Creates no users and changes no names or addresses.
/// </summary>
public class KeycloakUserRefresher {
    public const int PageSize = 200;

    private readonly IKeycloakAdminClient _keycloak;
    private readonly MemberRepository _members;
    private readonly UserRepository _users;
    private readonly KeycloakDepartmentChapterAssigner _departments;

    public KeycloakUserRefresher(IKeycloakAdminClient keycloak, MemberRepository members, UserRepository users, KeycloakDepartmentChapterAssigner departments) {
        _keycloak = keycloak;
        _members = members;
        _users = users;
        _departments = departments;
    }

    /// <summary>Throws a <see cref="KeycloakException"/> when Keycloak fails; what was applied before stays applied.</summary>
    public async Task<KeycloakUserRefreshDTO> RefreshAsync(DateTime nowUtc, CancellationToken ct) {
        var result = new KeycloakUserRefreshDTO();
        for (var first = 0; ; first += PageSize) {
            var page = await _keycloak.ListUsersAsync(first, PageSize, ct);
            foreach (var account in page) {
                result.AccountsRead++;
                Apply(account, nowUtc, result);
            }
            if (page.Count < PageSize)
                return result;
        }
    }

    private void Apply(KeycloakUserRepresentation account, DateTime nowUtc, KeycloakUserRefreshDTO result) {
        if (account.Attribute("closed_time") != null)
            return;
        if (!MemberNumberParser.TryParse(account.Attribute("member_number"), out var memberNumber))
            return;

        var member = _members.GetByMemberNumber(memberNumber);
        var user = member?.UserId == null ? null : _users.Get(member.UserId.Value);
        if (member == null || user == null) {
            result.AccountsWithoutUser++;
            return;
        }
        if (member.IsExited(nowUtc))
            return;

        if (user.KeycloakUserId != account.Id && account.Id.Length <= User.KeycloakUserIdMaxLength) {
            _users.SetKeycloakUserId(user.Id, account.Id);
            result.KeycloakIdsStored++;
        }
        if (SsoLoginHelper.FollowAccountVotingRights(member, SsoLoginHelper.ParseEligible(account.Attribute(KeycloakUserRepresentation.EligibleAttribute)), _members))
            result.VotingRightsChanged++;
        if (_departments.Assign(member, account.Attribute(KeycloakUserRepresentation.DepartmentAttribute)))
            result.ChaptersAssigned++;
    }
}
