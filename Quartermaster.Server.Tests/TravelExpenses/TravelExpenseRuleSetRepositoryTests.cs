using System;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.TravelExpenses;

public class TravelExpenseRuleSetRepositoryTests : IntegrationTestBase {
    private TravelExpenseRuleSet Seed(Guid? chapterId, DateOnly validFrom, string label) {
        var dto = TravelExpenseRulePresets.Federal();
        dto.ValidFrom = validFrom;
        dto.VersionLabel = label;
        var ruleSet = new TravelExpenseRuleSet { ChapterId = chapterId, UpdatedAt = DateTime.UtcNow };
        ruleSet.Apply(dto);
        Db.Insert(ruleSet);
        return ruleSet;
    }

    private TravelExpenseRuleSet? Resolve(Guid? chapterId, DateOnly date) {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        return services.GetRequiredService<TravelExpenseRuleSetRepository>().Resolve(chapterId, date, services.GetRequiredService<ChapterRepository>());
    }

    [Test]
    public async Task Without_chapter_rules_the_federal_default_applies() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");

        var rules = Resolve(chain[2].Id, new DateOnly(2026, 10, 1));

        await Assert.That(rules!.ChapterId).IsNull();
        await Assert.That(rules.VersionLabel).IsEqualTo("J2.0");
    }

    [Test]
    public async Task The_nearest_chapter_with_rules_wins() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        Seed(chain[1].Id, new DateOnly(2022, 4, 1), "LAND");

        var rules = Resolve(chain[2].Id, new DateOnly(2026, 10, 1));

        await Assert.That(rules!.VersionLabel).IsEqualTo("LAND");
    }

    [Test]
    public async Task A_version_not_yet_valid_falls_back_to_the_parent() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land");
        Seed(chain[1].Id, new DateOnly(2027, 1, 1), "FUTURE");

        var rules = Resolve(chain[1].Id, new DateOnly(2026, 10, 1));

        await Assert.That(rules!.VersionLabel).IsEqualTo("J2.0");
    }

    [Test]
    public async Task The_newest_valid_version_of_a_chapter_applies() {
        var chapter = Builder.SeedChapter("Land");
        Seed(chapter.Id, new DateOnly(2021, 1, 1), "OLD");
        Seed(chapter.Id, new DateOnly(2025, 1, 1), "NEW");

        await Assert.That(Resolve(chapter.Id, new DateOnly(2024, 6, 1))!.VersionLabel).IsEqualTo("OLD");
        await Assert.That(Resolve(chapter.Id, new DateOnly(2026, 6, 1))!.VersionLabel).IsEqualTo("NEW");
    }

    [Test]
    public async Task A_trip_before_every_global_version_gets_the_oldest() {
        var rules = Resolve(null, new DateOnly(2020, 1, 1));

        await Assert.That(rules!.VersionLabel).IsEqualTo("J2.0");
    }

    [Test]
    public async Task Rule_set_round_trips_rate_steps() {
        var chapter = Builder.SeedChapter("BW");
        var ruleSet = new TravelExpenseRuleSet { ChapterId = chapter.Id, UpdatedAt = DateTime.UtcNow };
        ruleSet.Apply(TravelExpenseRulePresets.BadenWuerttemberg());
        Db.Insert(ruleSet);

        var dto = Resolve(chapter.Id, new DateOnly(2026, 1, 1))!.ToDto();

        await Assert.That(dto.CarRates.Count).IsEqualTo(2);
        await Assert.That(dto.CarRates[0].UpToKm).IsEqualTo(20m);
        await Assert.That(dto.CarRates[1].RatePerKm).IsEqualTo(0.35m);
        await Assert.That(dto.MotorcycleRates).IsEmpty();
    }
}
