using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

/// <summary>The account menu shows the voting rights, so the session carries them for accounts linked to a member.</summary>
public class SessionVotingRightsTests : IntegrationTestBase {
    private async Task<LoginUserInfo> SessionUserOf(string token) {
        using var client = AuthenticatedClient(token);
        return (await client.GetFromJsonAsync<LoginResponse>("/api/users/session"))!.User;
    }

    [Test]
    public async Task Session_carries_the_voting_rights_of_the_linked_member() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(userId: user.Id);
        Db.Members.Where(m => m.Id == member.Id).Set(m => m.HasVotingRights, true).Set(m => m.OpenFeeTotal, 24m).Update();

        var session = await SessionUserOf(token);

        await Assert.That(session.HasVotingRights).IsEqualTo(true);
        await Assert.That(session.OpenFeeTotal).IsEqualTo(24m);
    }

    [Test]
    public async Task Session_without_member_has_no_voting_rights() {
        var (_, token) = Builder.SeedAuthenticatedUser();

        var session = await SessionUserOf(token);

        await Assert.That(session.HasVotingRights).IsNull();
        await Assert.That(session.OpenFeeTotal).IsNull();
    }
}
