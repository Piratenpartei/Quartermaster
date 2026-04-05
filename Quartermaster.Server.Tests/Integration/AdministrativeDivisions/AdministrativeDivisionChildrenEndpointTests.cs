using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AdministrativeDivisions;

public class AdministrativeDivisionChildrenEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_children_of_parent() {
        var parent = Builder.SeedAdminDivision("Parent", depth: 1);
        Builder.SeedAdminDivision("Child A", depth: 2, parentId: parent.Id);
        Builder.SeedAdminDivision("Child B", depth: 2, parentId: parent.Id);
        Builder.SeedAdminDivision("Other Root", depth: 1);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/{parent.Id}/children");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_list_for_leaf() {
        var leaf = Builder.SeedAdminDivision("Leaf", depth: 3);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/{leaf.Id}/children");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list!.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_empty_list_for_nonexistent_parent() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/{Guid.NewGuid()}/children");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list!.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Children_sorted_alphabetically_by_name() {
        var parent = Builder.SeedAdminDivision("Parent", depth: 1);
        Builder.SeedAdminDivision("Zulu", depth: 2, parentId: parent.Id);
        Builder.SeedAdminDivision("Alpha", depth: 2, parentId: parent.Id);
        Builder.SeedAdminDivision("Mike", depth: 2, parentId: parent.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/{parent.Id}/children");
        var list = await response.Content.ReadFromJsonAsync<AdministrativeDivisionDTO[]>();
        await Assert.That(list![0].Name).IsEqualTo("Alpha");
        await Assert.That(list[1].Name).IsEqualTo("Mike");
        await Assert.That(list[2].Name).IsEqualTo("Zulu");
    }

    [Test]
    public async Task Anonymous_access_allowed() {
        var parent = Builder.SeedAdminDivision("Parent", depth: 1);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/{parent.Id}/children");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
