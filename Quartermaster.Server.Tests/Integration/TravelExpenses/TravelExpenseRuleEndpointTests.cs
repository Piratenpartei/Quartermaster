using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.TravelExpenses;

public class TravelExpenseRuleEndpointTests : IntegrationTestBase {
    private static TravelExpenseRulesDTO Rules(Guid? chapterId, DateOnly validFrom) {
        var rules = TravelExpenseRulePresets.LowerSaxony();
        rules.ChapterId = chapterId;
        rules.ValidFrom = validFrom;
        return rules;
    }

    private async Task<HttpClient> ChapterEditorAsync(Guid chapterId) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapterId] = [PermissionIdentifier.EditTravelExpenseRules]
        });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private async Task<HttpClient> GlobalEditorAsync() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditTravelExpenseRules]);
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    [Test]
    public async Task Chapter_editor_creates_a_version_for_the_own_chapter_only() {
        var state = Builder.SeedChapter("Niedersachsen");
        var county = Builder.SeedChapter("Hannover", parentChapterId: state.Id);
        using var client = await ChapterEditorAsync(state.Id);

        var response = await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(state.Id, new DateOnly(2026, 1, 1)));
        var subChapter = await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(county.Id, new DateOnly(2026, 1, 1)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(subChapter.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        var listed = await client.GetFromJsonAsync<List<TravelExpenseRulesDTO>>($"/api/travel-expense-rules?chapterId={state.Id}");
        await Assert.That(listed!.Single().MotorcycleRates.Single().RatePerKm).IsEqualTo(0.13m);
    }

    [Test]
    public async Task Chapter_editor_cannot_touch_other_chapters_or_the_global_default() {
        var own = Builder.SeedChapter("Bayern");
        var other = Builder.SeedChapter("Hessen");
        using var client = await ChapterEditorAsync(own.Id);

        var foreign = await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(other.Id, new DateOnly(2026, 1, 1)));
        var global = await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(null, new DateOnly(2026, 1, 1)));
        var listGlobal = await client.GetAsync("/api/travel-expense-rules");

        await Assert.That(foreign.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(global.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(listGlobal.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Scopes_list_only_the_chapters_with_a_direct_grant() {
        var state = Builder.SeedChapter("Niedersachsen");
        var county = Builder.SeedChapter("Hannover", parentChapterId: state.Id);
        using var client = await ChapterEditorAsync(state.Id);

        var scopes = await client.GetFromJsonAsync<TravelExpenseRuleScopesDTO>("/api/travel-expense-rules/scopes");

        await Assert.That(scopes!.CanEditGlobal).IsFalse();
        await Assert.That(scopes.ChapterIds!).Contains(state.Id);
        await Assert.That(scopes.ChapterIds!).DoesNotContain(county.Id);
    }

    [Test]
    public async Task A_second_version_from_the_same_day_is_refused() {
        var chapter = Builder.SeedChapter("Bayern");
        using var client = await ChapterEditorAsync(chapter.Id);
        var first = await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(chapter.Id, new DateOnly(2026, 1, 1)));
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(chapter.Id, new DateOnly(2026, 1, 1)));

        await Assert.That((int)second.StatusCode).IsEqualTo(409);
    }

    [Test]
    public async Task Rate_steps_without_an_open_last_step_are_refused() {
        var chapter = Builder.SeedChapter("Bayern");
        using var client = await ChapterEditorAsync(chapter.Id);
        var rules = Rules(chapter.Id, new DateOnly(2026, 1, 1));
        rules.CarRates = [new RateStepDTO { UpToKm = 20, RatePerKm = 0.30m }];

        var response = await client.PostAsJsonAsync("/api/travel-expense-rules", rules);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_keeps_the_chapter_and_changes_the_rates() {
        var chapter = Builder.SeedChapter("Bayern");
        using var client = await ChapterEditorAsync(chapter.Id);
        var created = await (await client.PostAsJsonAsync("/api/travel-expense-rules", Rules(chapter.Id, new DateOnly(2026, 1, 1))))
            .Content.ReadFromJsonAsync<TravelExpenseRulesDTO>();
        created!.CarRates = [new RateStepDTO { RatePerKm = 0.35m }];

        var updated = await client.PutAsJsonAsync($"/api/travel-expense-rules/{created.Id}", created);
        created.ChapterId = null;
        var moved = await client.PutAsJsonAsync($"/api/travel-expense-rules/{created.Id}", created);

        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(moved.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var listed = await client.GetFromJsonAsync<List<TravelExpenseRulesDTO>>($"/api/travel-expense-rules?chapterId={chapter.Id}");
        await Assert.That(listed!.Single().CarRates.Single().RatePerKm).IsEqualTo(0.35m);
    }

    [Test]
    public async Task The_last_global_version_cannot_be_deleted() {
        using var client = await GlobalEditorAsync();
        var globals = await client.GetFromJsonAsync<List<TravelExpenseRulesDTO>>("/api/travel-expense-rules");

        var response = await client.DeleteAsync($"/api/travel-expense-rules/{globals!.Single().Id}");

        await Assert.That((int)response.StatusCode).IsEqualTo(409);
    }

    [Test]
    public async Task Presets_need_the_permission_somewhere() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var plain = await AuthenticatedClientWithCsrfAsync(token);
        using var editor = await GlobalEditorAsync();

        var refused = await plain.GetAsync("/api/travel-expense-rules/presets");
        var presets = await editor.GetFromJsonAsync<List<TravelExpenseRulePresetDTO>>("/api/travel-expense-rules/presets");

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(presets!.Count).IsEqualTo(TravelExpenseRulePresets.All.Count);
    }
}
