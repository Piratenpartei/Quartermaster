using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AdministrativeDivisions;

public class AdministrativeDivisionRootsEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_access_allowed_returns_empty_list_when_none() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/roots");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_only_depth_1_entries_as_roots() {
        // Per the repository: GetRoots returns entries with Depth == 1 (depth 0 is Null Island).
        Builder.SeedAdminDivision("Root Country A", depth: 1);
        Builder.SeedAdminDivision("Root Country B", depth: 1);
        Builder.SeedAdminDivision("Depth 0 Island", depth: 0);
        Builder.SeedAdminDivision("Child", depth: 2);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/roots");
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list!.Length).IsEqualTo(2);
        await Assert.That(list.All(d => d.Depth == 1)).IsTrue();
    }

    [Test]
    public async Task Sorted_alphabetically_by_name() {
        Builder.SeedAdminDivision("Zulu", depth: 1);
        Builder.SeedAdminDivision("Alpha", depth: 1);
        Builder.SeedAdminDivision("Mike", depth: 1);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/roots");
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list![0].Name).IsEqualTo("Alpha");
        await Assert.That(list[1].Name).IsEqualTo("Mike");
        await Assert.That(list[2].Name).IsEqualTo("Zulu");
    }

    [Test]
    public async Task Returns_dto_fields_correctly() {
        Builder.SeedAdminDivision("Named", depth: 1, adminCode: "XX", postCodes: "12345");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/roots");
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list!.Length).IsEqualTo(1);
        await Assert.That(list[0].Name).IsEqualTo("Named");
        await Assert.That(list[0].AdminCode).IsEqualTo("XX");
        await Assert.That(list[0].PostCodes).IsEqualTo("12345");
        await Assert.That(list[0].Depth).IsEqualTo(1);
    }
}
