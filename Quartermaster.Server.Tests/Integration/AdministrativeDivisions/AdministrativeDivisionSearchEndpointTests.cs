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
