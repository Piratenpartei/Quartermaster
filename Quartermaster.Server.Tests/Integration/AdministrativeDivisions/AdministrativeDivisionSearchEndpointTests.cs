using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AdministrativeDivisions;

public class AdministrativeDivisionSearchEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_access_allowed() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_paginated_results() {
        for (var i = 1; i <= 30; i++) {
            Builder.SeedAdminDivision($"ZPage {i:D3}", depth: 1);
        }
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Page=1&PageSize=10&Query=ZPage");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(10);
        await Assert.That(result.TotalCount).IsEqualTo(30);
    }

    [Test]
    public async Task Query_filters_by_name() {
        Builder.SeedAdminDivision("Hannover", depth: 1);
        Builder.SeedAdminDivision("Berlin", depth: 1);
        Builder.SeedAdminDivision("München", depth: 1);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=Han");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Hannover");
    }

    [Test]
    public async Task Query_filters_by_postcode() {
        Builder.SeedAdminDivision("Hannover", depth: 1, postCodes: "30159");
        Builder.SeedAdminDivision("Berlin", depth: 1, postCodes: "10115");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=30159");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Hannover");
    }

    [Test]
    public async Task Query_filters_by_postcode_prefix() {
        Builder.SeedAdminDivision("Wiesbaden", depth: 7, postCodes: "65183,65185,65187");
        Builder.SeedAdminDivision("Mainz", depth: 7, postCodes: "55116,55118");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=6518");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Wiesbaden");
    }

    [Test]
    public async Task Query_does_not_match_postcode_infix() {
        Builder.SeedAdminDivision("Wiesbaden", depth: 7, postCodes: "65183,65185");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=5183");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Query_filters_by_admin_code() {
        Builder.SeedAdminDivision("Wiesbaden", depth: 7, adminCode: "06414000");
        Builder.SeedAdminDivision("Mainz", depth: 7, adminCode: "07315000");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=06414000");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Wiesbaden");
    }

    [Test]
    public async Task Query_filters_by_admin_code_prefix() {
        Builder.SeedAdminDivision("Wiesbaden", depth: 7, adminCode: "06414000");
        Builder.SeedAdminDivision("Darmstadt", depth: 7, adminCode: "06411000");
        Builder.SeedAdminDivision("Mainz", depth: 7, adminCode: "07315000");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=0641");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_list_when_no_matches() {
        Builder.SeedAdminDivision("Hannover", depth: 1);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Query=Zzz");
        var result = await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(0);
        await Assert.That(result.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task Rejects_page_size_over_100() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Page=1&PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_negative_page() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/administrativedivisions/search?Page=-1&PageSize=10");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
