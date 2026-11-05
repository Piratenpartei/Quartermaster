using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Users;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Users;

/// <summary>
/// Applies every Keycloak account to its existing user the way a login would, without the session: stores the Keycloak id and the Keycloak username,
/// lets the voting rights of members the import hasn't delivered yet follow the account, and maps the department to a chapter. Creates no users and
/// changes no personal names or addresses.
/// </summary>
public class KeycloakUserRefresher {
    public const int PageSize = 200;

    private readonly IKeycloakAdminClient _keycloak;
    private readonly MemberRepository _members;
    private readonly UserRepository _users;
    private readonly KeycloakDepartmentChapterAssigner _departments;
    private readonly ILogger<KeycloakUserRefresher> _logger;

    public KeycloakUserRefresher(IKeycloakAdminClient keycloak, MemberRepository members, UserRepository users, KeycloakDepartmentChapterAssigner departments,
        ILogger<KeycloakUserRefresher> logger) {
        _keycloak = keycloak;
        _members = members;
        _users = users;
        _departments = departments;
        _logger = logger;
    }

    /// <summary>Throws a <see cref="KeycloakException"/> when Keycloak fails; what was applied before stays applied.</summary>
    public async Task<KeycloakUserRefreshDTO> RefreshAsync(DateTime nowUtc, CancellationToken ct) {
        var result = new KeycloakUserRefreshDTO();
        var skippedUsernames = new Dictionary<Guid, string>();
        for (var first = 0; ; first += PageSize) {
            var page = await _keycloak.ListUsersAsync(first, PageSize, ct);
            foreach (var account in page) {
                result.AccountsRead++;
                Apply(account, nowUtc, result, skippedUsernames);
            }
            if (page.Count < PageSize)
                break;
        }
        RetrySkippedUsernames(skippedUsernames, result);
        return result;
    }

    /// <summary>An account renamed later in the list frees the name an earlier one was refused, so the refused ones get a second try.</summary>
    private void RetrySkippedUsernames(Dictionary<Guid, string> skippedUsernames, KeycloakUserRefreshDTO result) {
        foreach (var (userId, username) in skippedUsernames) {
            if (_users.SetUsernameFromSso(userId, username) == UsernameUpdate.Set) {
                result.UsernamesSet++;
                continue;
            }
            result.UsernamesSkipped++;
            _logger.LogWarning("Keycloak username of user {UserId} not set: another account still holds it", userId);
        }
    }

    private void Apply(KeycloakUserRepresentation account, DateTime nowUtc, KeycloakUserRefreshDTO result, Dictionary<Guid, string> skippedUsernames) {
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
        ApplyUsername(user.Id, account.Username, result, skippedUsernames);
        if (SsoLoginHelper.FollowAccountVotingRights(member, SsoLoginHelper.ParseEligible(account.Attribute(KeycloakUserRepresentation.EligibleAttribute)), _members))
            result.VotingRightsChanged++;
        if (_departments.Assign(member, account.Attribute(KeycloakUserRepresentation.DepartmentAttribute)))
            result.ChaptersAssigned++;
    }

    private void ApplyUsername(Guid userId, string username, KeycloakUserRefreshDTO result, Dictionary<Guid, string> skippedUsernames) {
        if (string.IsNullOrWhiteSpace(username))
            return;
        var outcome = _users.SetUsernameFromSso(userId, username);
        if (outcome == UsernameUpdate.Set) {
            result.UsernamesSet++;
        } else if (outcome == UsernameUpdate.Taken) {
            skippedUsernames[userId] = username;
        } else if (outcome == UsernameUpdate.TooLong) {
            result.UsernamesSkipped++;
            _logger.LogWarning("Keycloak username of user {UserId} not set: longer than {MaxLength} characters", userId, User.UsernameMaxLength);
        }
    }
}
