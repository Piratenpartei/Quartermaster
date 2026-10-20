using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionPrefillEndpoint : EndpointWithoutRequest<DueSelectionPrefillDTO> {
    private readonly MemberRepository _members;
    private readonly UserRepository _users;
    private readonly PermissionContext _perms;

    public DueSelectionPrefillEndpoint(MemberRepository members, UserRepository users, PermissionContext perms) {
        _members = members;
        _users = users;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/dueselector/prefill");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = _users.Get(_perms.UserId.Value);
        var member = _members.GetByUserIdIncludingFloating(_perms.UserId.Value);
        if (member != null) {
            await SendAsync(new DueSelectionPrefillDTO {
                FirstName = member.FirstName,
                LastName = member.LastName,
                Email = string.IsNullOrWhiteSpace(member.Email) ? user?.Email ?? "" : member.Email,
                MemberNumber = member.MemberNumber,
                Locked = true
            }, cancellation: ct);
            return;
        }

        await SendAsync(new DueSelectionPrefillDTO {
            FirstName = user?.FirstName ?? "",
            LastName = user?.LastName ?? "",
            Email = user?.Email ?? "",
            Locked = false
        }, cancellation: ct);
    }
}
