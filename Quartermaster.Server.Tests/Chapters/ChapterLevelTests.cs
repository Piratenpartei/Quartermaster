using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB.Data;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Migrations;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Chapters;

public class ChapterLevelTests : IntegrationTestBase {
    private async Task<HttpClient> EditorClientAsync() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditChapter, PermissionIdentifier.CreateChapter]);
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    [Test]
    public async Task Effective_period_defaults_by_level() {
        var parent = Guid.NewGuid();
        await Assert.That(new Chapter { ParentChapterId = parent, Level = ChapterLevel.State }.EffectiveTakeOverPeriodDays()).IsEqualTo(28);
        await Assert.That(new Chapter { Level = ChapterLevel.State }.EffectiveTakeOverPeriodDays()).IsEqualTo(62);
        await Assert.That(new Chapter { Level = null }.EffectiveTakeOverPeriodDays()).IsEqualTo(62);
        await Assert.That(new Chapter { ParentChapterId = parent, Level = ChapterLevel.County }.EffectiveTakeOverPeriodDays()).IsNull();
        await Assert.That(new Chapter { ParentChapterId = parent, Level = ChapterLevel.State, TakeOverPeriodDays = 10 }.EffectiveTakeOverPeriodDays()).IsEqualTo(10);
        await Assert.That(new Chapter { ParentChapterId = parent, Level = ChapterLevel.County, TakeOverPeriodDays = 7 }.EffectiveTakeOverPeriodDays()).IsEqualTo(7);
    }

    [Test]
    public async Task Migration_prefill_sets_federal_state_and_leaves_deeper_levels() {
        var root = Builder.SeedChapter("Bund");
        var child = Builder.SeedChapter("Land", root.Id);
        var grandchild = Builder.SeedChapter("Kreis", child.Id);

        Db.Execute(M009.PrefillFederalLevelSql);
        Db.Execute(M009.PrefillStateLevelSql);

        await Assert.That(Db.Chapters.Single(c => c.Id == root.Id).Level).IsEqualTo(ChapterLevel.Federal);
        await Assert.That(Db.Chapters.Single(c => c.Id == child.Id).Level).IsEqualTo(ChapterLevel.State);
        await Assert.That(Db.Chapters.Single(c => c.Id == grandchild.Id).Level).IsNull();
    }

    [Test]
    public async Task Create_root_gets_federal() {
        using var client = await EditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "Bund", Level = ChapterLevel.County });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ChapterDTO>();
        await Assert.That(dto!.Level).IsEqualTo(ChapterLevel.Federal);
        await Assert.That(Db.Chapters.Single(c => c.Id == dto.Id).Level).IsEqualTo(ChapterLevel.Federal);
    }

    [Test]
    public async Task Create_refuses_federal_below_root() {
        var root = Builder.SeedChapter("Bund");
        using var client = await EditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "LV", ParentChapterId = root.Id, Level = ChapterLevel.Federal });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Chapter.FederalRootOnly);
    }

    [Test]
    public async Task Create_refuses_unknown_level() {
        var root = Builder.SeedChapter("Bund");
        using var client = await EditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "LV", ParentChapterId = root.Id, Level = (ChapterLevel)99 });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Chapter.LevelInvalid);
        await Assert.That(Db.Chapters.Count(c => c.Name == "LV")).IsEqualTo(0);
    }

    [Test]
    public async Task Update_refuses_unknown_level() {
        var root = Builder.SeedChapter("Bund");
        var chapter = Builder.SeedChapter("LV", root.Id);
        using var client = await EditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "LV", ParentChapterId = root.Id, Level = (ChapterLevel)99 });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Chapter.LevelInvalid);
        await Assert.That(Db.Chapters.Single(c => c.Id == chapter.Id).Level).IsNull();
    }

    [Test]
    public async Task Update_forces_federal_on_new_root() {
        var root = Builder.SeedChapter("Bund");
        var chapter = Builder.SeedChapter("LV", root.Id);
        using var client = await EditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "LV", Level = ChapterLevel.State });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Chapters.Single(c => c.Id == chapter.Id).Level).IsEqualTo(ChapterLevel.Federal);
    }

    [Test]
    public async Task Update_refuses_federal_below_root() {
        var root = Builder.SeedChapter("Bund");
        var chapter = Builder.SeedChapter("LV", root.Id);
        using var client = await EditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "LV", ParentChapterId = root.Id, Level = ChapterLevel.Federal });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Chapter.FederalRootOnly);
        await Assert.That(Db.Chapters.Single(c => c.Id == chapter.Id).Level).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(366)]
    public async Task Update_refuses_period_out_of_range(int days) {
        var root = Builder.SeedChapter("Bund");
        var chapter = Builder.SeedChapter("LV", root.Id);
        using var client = await EditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "LV", ParentChapterId = root.Id, TakeOverPeriodDays = days });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Chapter.TakeOverPeriodRange);
    }

    [Test]
    [Arguments(1)]
    [Arguments(365)]
    public async Task Update_accepts_period_bounds(int days) {
        var root = Builder.SeedChapter("Bund");
        var chapter = Builder.SeedChapter("LV", root.Id);
        using var client = await EditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { IncludesContactAndTakeOver = true, Name = "LV", ParentChapterId = root.Id, TakeOverPeriodDays = days });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Chapters.Single(c => c.Id == chapter.Id).TakeOverPeriodDays).IsEqualTo(days);
    }

    [Test]
    [Arguments(0)]
    [Arguments(366)]
    public async Task Create_refuses_period_out_of_range(int days) {
        var root = Builder.SeedChapter("Bund");
        using var client = await EditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "LV", ParentChapterId = root.Id, TakeOverPeriodDays = days });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Chapter.TakeOverPeriodRange);
    }

    [Test]
    public async Task Update_stores_level_and_period() {
        var root = Builder.SeedChapter("Bund");
        var chapter = Builder.SeedChapter("LV", root.Id);
        using var client = await EditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest {
            IncludesContactAndTakeOver = true,
            Name = "LV",
            ParentChapterId = root.Id,
            Level = ChapterLevel.State,
            TakeOverPeriodDays = 40
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stored = Db.Chapters.Single(c => c.Id == chapter.Id);
        await Assert.That(stored.Level).IsEqualTo(ChapterLevel.State);
        await Assert.That(stored.TakeOverPeriodDays).IsEqualTo(40);
    }

    [Test]
    public async Task Dto_carries_level_and_period() {
        var chapter = new Chapter { Id = Guid.NewGuid(), Name = "KV", ParentChapterId = Guid.NewGuid(), Level = ChapterLevel.County, TakeOverPeriodDays = 14 };

        var dto = chapter.ToDto();

        await Assert.That(dto.Level).IsEqualTo(ChapterLevel.County);
        await Assert.That(dto.TakeOverPeriodDays).IsEqualTo(14);
    }
}
