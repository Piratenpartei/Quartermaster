using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.TestData;

public class TestDataSeeder {
    private readonly DbContext _context;
    private readonly ChapterRepository _chapterRepo;

    public TestDataSeeder(DbContext context, ChapterRepository chapterRepo) {
        _context = context;
        _chapterRepo = chapterRepo;
    }

    public int Seed(int applicationCount = 50, int standaloneSelectionCount = 20) {
        var chapters = _chapterRepo.GetAll();
        if (chapters.Count == 0)
            return 0;

        // State chapters only (not Bundesverband)
        var stateChapters = chapters.Where(c => c.ParentChapterId != null).ToList();

        var germanCities = new[] {
            "Berlin", "Hamburg", "München", "Köln", "Frankfurt am Main", "Stuttgart",
            "Düsseldorf", "Leipzig", "Dortmund", "Essen", "Bremen", "Dresden",
            "Hannover", "Nürnberg", "Duisburg", "Bochum", "Wuppertal", "Bielefeld",
            "Bonn", "Münster", "Mannheim", "Karlsruhe", "Augsburg", "Wiesbaden",
            "Mönchengladbach", "Braunschweig", "Kiel", "Aachen", "Rostock", "Lübeck"
        };

        var germanStreets = new[] {
            "Hauptstraße", "Bahnhofstraße", "Schulstraße", "Gartenstraße", "Berliner Straße",
            "Ringstraße", "Bergstraße", "Waldstraße", "Dorfstraße", "Kirchstraße",
            "Friedrichstraße", "Schillerstraße", "Goethestraße", "Bismarckstraße",
            "Lindenstraße", "Mühlenweg", "Rosenweg", "Feldstraße", "Parkstraße", "Bachstraße"
        };

        var justifications = new[] {
            "Ich bin derzeit Student und habe kein Einkommen.",
            "Ich bin arbeitslos und beziehe ALG II.",
            "Ich befinde mich in einer Ausbildung mit geringem Einkommen.",
            "Ich bin Rentner mit Grundsicherung.",
            "Ich bin alleinerziehend mit zwei Kindern.",
            "Mein Einkommen reicht gerade für die Miete.",
            "Ich bin in einer Umschulung und habe kaum Einkommen.",
            "Ich beziehe Wohngeld und habe finanzielle Schwierigkeiten."
        };

        Randomizer.Seed = new Random(42);
        var faker = new Faker("de");
        var created = 0;

        // Create membership applications with linked due selections
        for (var i = 0; i < applicationCount; i++) {
            var chapter = faker.PickRandom(stateChapters);
            var firstName = faker.Name.FirstName();
            var lastName = faker.Name.LastName();
            var email = faker.Internet.Email(firstName, lastName);
            var isReduced = faker.Random.Bool(0.3f);

            var dueSelection = new DueSelection {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                EMail = email,
                SelectedValuation = isReduced
                    ? SelectedValuation.Reduced
                    : faker.PickRandom(SelectedValuation.MonthlyPayGroup, SelectedValuation.OnePercentYearlyPay),
                YearlyIncome = faker.Random.Decimal(12000, 80000),
                MonthlyIncomeGroup = faker.PickRandom(0m, 1000m, 2000m, 2500m, 3000m, 4000m, 5000m, 6000m),
                ReducedAmount = isReduced ? faker.Random.Decimal(1, 60) : 0,
                SelectedDue = isReduced ? faker.Random.Decimal(1, 60) : faker.PickRandom(72m, 120m, 180m, 240m, 360m, 480m, 600m),
                ReducedJustification = isReduced ? faker.PickRandom(justifications) : "",
                ReducedTimeSpan = isReduced
                    ? faker.PickRandom<ReducedTimeSpan>()
                    : ReducedTimeSpan.OneYear,
                IsDirectDeposit = faker.Random.Bool(0.6f),
                AccountHolder = $"{firstName} {lastName}",
                IBAN = $"DE{faker.Random.Long(10000000000000000, 99999999999999999)}",
                PaymentSchedule = faker.PickRandom(PaymentScedule.Annual, PaymentScedule.Quarterly),
                Status = isReduced
                    ? DueSelectionStatus.Pending
                    : DueSelectionStatus.AutoApproved
            };

            _context.Insert(dueSelection);

            var submittedAt = faker.Date.Between(
                DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow);

            var application = new MembershipApplication {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = faker.Date.Between(
                    DateTime.Now.AddYears(-70), DateTime.Now.AddYears(-14)),
                Citizenship = faker.Random.Bool(0.95f) ? "Deutsch" : faker.PickRandom("Österreichisch", "Schweizerisch", "Polnisch", "Türkisch"),
                EMail = email,
                PhoneNumber = faker.Phone.PhoneNumber("0### #######"),
                AddressStreet = faker.PickRandom(germanStreets),
                AddressHouseNbr = faker.Random.Int(1, 150).ToString(),
                AddressPostCode = faker.Random.Int(10000, 99999).ToString(),
                AddressCity = faker.PickRandom(germanCities),
                ChapterId = chapter.Id,
                DueSelectionId = dueSelection.Id,
                ConformityDeclarationAccepted = true,
                HasPriorDeclinedApplication = faker.Random.Bool(0.05f),
                IsMemberOfAnotherParty = faker.Random.Bool(0.1f),
                ApplicationText = faker.Random.Bool(0.3f)
                    ? faker.Lorem.Sentence(faker.Random.Int(5, 20))
                    : "",
                EntryDate = submittedAt,
                SubmittedAt = submittedAt,
                Status = faker.PickRandom(
                    ApplicationStatus.Pending, ApplicationStatus.Pending, ApplicationStatus.Pending,
                    ApplicationStatus.Approved, ApplicationStatus.Rejected)
            };

            _context.Insert(application);
            created++;
        }

        // Create standalone due selections (existing members re-classifying)
        for (var i = 0; i < standaloneSelectionCount; i++) {
            var firstName = faker.Name.FirstName();
            var lastName = faker.Name.LastName();
            var isReduced = faker.Random.Bool(0.5f);

            var dueSelection = new DueSelection {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                EMail = faker.Internet.Email(firstName, lastName),
                MemberNumber = faker.Random.Int(10000, 99999),
                SelectedValuation = isReduced
                    ? SelectedValuation.Reduced
                    : faker.PickRandom(SelectedValuation.MonthlyPayGroup, SelectedValuation.OnePercentYearlyPay),
                YearlyIncome = faker.Random.Decimal(12000, 80000),
                MonthlyIncomeGroup = faker.PickRandom(0m, 2000m, 3000m, 4000m, 5000m),
                ReducedAmount = isReduced ? faker.Random.Decimal(1, 60) : 0,
                SelectedDue = isReduced ? faker.Random.Decimal(1, 60) : faker.PickRandom(72m, 120m, 180m, 240m),
                ReducedJustification = isReduced ? faker.PickRandom(justifications) : "",
                ReducedTimeSpan = isReduced
                    ? faker.PickRandom<ReducedTimeSpan>()
                    : ReducedTimeSpan.OneYear,
                IsDirectDeposit = faker.Random.Bool(0.5f),
                AccountHolder = $"{firstName} {lastName}",
                IBAN = $"DE{faker.Random.Long(10000000000000000, 99999999999999999)}",
                PaymentSchedule = faker.PickRandom(PaymentScedule.Annual, PaymentScedule.Quarterly),
                Status = isReduced
                    ? DueSelectionStatus.Pending
                    : DueSelectionStatus.AutoApproved
            };

            _context.Insert(dueSelection);
            created++;
        }

        return created;
    }
}
