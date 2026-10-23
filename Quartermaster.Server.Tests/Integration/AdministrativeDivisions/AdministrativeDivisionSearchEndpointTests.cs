using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Server.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AdministrativeDivisions;

public class AdministrativeDivisionSearchEndpointTests : IntegrationTestBase {
    /// <summary>Searches after dropping the index, which otherwise still holds the divisions of an earlier test.</summary>
    private async Task<AdministrativeDivisionSearchResponse> Search(string query, int pageSize = 25) {
        Factory.Services.GetRequiredService<AdministrativeDivisionSearchIndex>().Invalidate();
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/search?Page=1&PageSize={pageSize}&Query={Uri.EscapeDataString(query)}");
        return (await response.Content.ReadFromJsonAsync<AdministrativeDivisionSearchResponse>())!;
    }

    private void SeedName(AdministrativeDivision division, string name, string? language = null) {
        Db.Insert(new AdministrativeDivisionName {
            AdministrativeDivisionId = division.Id,
            Name = name,
            Language = language,
            Source = AdministrativeDivisionNameSource.GeoNamesAlternate
        });
    }

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
        var result = await Search("ZPage", pageSize: 10);
        await Assert.That(result.Items.Count).IsEqualTo(10);
        await Assert.That(result.TotalCount).IsEqualTo(30);
    }

    [Test]
    public async Task Query_filters_by_name() {
        Builder.SeedAdminDivision("Hannover", depth: 1);
        Builder.SeedAdminDivision("Berlin", depth: 1);
        Builder.SeedAdminDivision("München", depth: 1);
        var result = await Search("Han");
        await Assert.That(result.Items.Count).IsEqualTo(1);
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
        var result = await Search("Zzz");
        await Assert.That(result.Items.Count).IsEqualTo(0);
        await Assert.That(result.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task Finds_a_division_by_another_name_and_says_which() {
        var county = Builder.SeedAdminDivision("Landkreis Spree-Neiße", depth: 6, adminCode: "12071");
        SeedName(county, "Wokrejs Sprjewja-Nysa", "hsb");

        var result = await Search("Sprjewja");

        await Assert.That(result.Items.Single().Name).IsEqualTo("Landkreis Spree-Neiße");
        await Assert.That(result.Items.Single().MatchedName).IsEqualTo("Wokrejs Sprjewja-Nysa");
    }

    [Test]
    public async Task Shown_name_match_reports_no_other_name() {
        var county = Builder.SeedAdminDivision("Landkreis Spree-Neiße", depth: 6);
        SeedName(county, "Spree-Neisse");

        var result = await Search("Spree-Neiße");

        await Assert.That(result.Items.Single().MatchedName).IsNull();
    }

    [Test]
    [Arguments("Spree Neise")]
    [Arguments("spreeneisse")]
    [Arguments("Spree-Nei")]
    [Arguments("Neiße")]
    [Arguments("Spree-Neisse")]
    public async Task Tolerates_typos_missing_umlauts_and_partly_typed_words(string query) {
        Builder.SeedAdminDivision("Landkreis Spree-Neiße", depth: 6);
        Builder.SeedAdminDivision("Berlin", depth: 4);

        var result = await Search(query);

        await Assert.That(result.Items.Select(i => i.Name)).IsEquivalentTo(["Landkreis Spree-Neiße"]);
    }

    [Test]
    public async Task Designator_words_only_count_when_searched_for() {
        Builder.SeedAdminDivision("Landkreis Göttingen", depth: 6);
        Builder.SeedAdminDivision("Göttingen, Stadt", depth: 7);

        var plain = await Search("Göttingen");
        var county = await Search("Landkreis Göttingen");

        await Assert.That(plain.Items.Select(i => i.Name)).IsEquivalentTo(["Landkreis Göttingen", "Göttingen, Stadt"]);
        await Assert.That(county.Items.Select(i => i.Name)).IsEquivalentTo(["Landkreis Göttingen"]);
    }

    [Test]
    public async Task Exact_matches_rank_before_typos_and_higher_levels_first() {
        Builder.SeedAdminDivision("Hildesheim", depth: 8);
        Builder.SeedAdminDivision("Hildesheim", depth: 7);
        Builder.SeedAdminDivision("Hildesheimer Wald", depth: 8);
        Builder.SeedAdminDivision("Hildeshiem", depth: 6);

        var result = await Search("Hildesheim ");

        await Assert.That(string.Join(",", result.Items.Select(i => i.Depth))).IsEqualTo("7,8,8,6");
    }

    [Test]
    public async Task Words_without_umlauts_match_exactly() {
        Builder.SeedAdminDivision("München", depth: 7);
        Builder.SeedAdminDivision("Mauchen", depth: 8);

        var plain = await Search("Munchen ");
        var written = await Search("Muenchen ");

        await Assert.That(plain.Items.Select(i => i.Name)).IsEquivalentTo(["München"]);
        await Assert.That(written.Items.Select(i => i.Name)).IsEquivalentTo(["München"]);
    }

    [Test]
    public async Task Partly_typed_word_does_not_match_designators() {
        Builder.SeedAdminDivision("Hamburg, Freie und Hansestadt", depth: 6);
        Builder.SeedAdminDivision("Hannover", depth: 7);

        var result = await Search("Han");

        await Assert.That(result.Items.Select(i => i.Name)).IsEquivalentTo(["Hannover"]);
    }

    [Test]
    public async Task Rejects_overlong_query() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/administrativedivisions/search?Query={new string('a', 201)}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
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
