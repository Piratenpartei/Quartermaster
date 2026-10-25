using Quartermaster.Server.Forwarding.SageEntry;

namespace Quartermaster.Server.Tests.Forwarding;

public class SageDueTests {
    [Test]
    [Arguments("2026-01-15", 120, 120)]
    [Arguments("2026-10-01", 120, 30)]
    [Arguments("2026-12-31", 120, 10)]
    [Arguments("2026-11-01", 100, 16.67)]
    public async Task Partial_counts_the_months_from_the_entry_month(string entryDate, double yearly, double expected) {
        var partial = SageDue.Partial((decimal)yearly, DateOnly.Parse(entryDate));

        await Assert.That(partial).IsEqualTo((decimal)expected);
    }

    [Test]
    public async Task MonthName_is_german() {
        await Assert.That(SageDue.MonthName(11)).IsEqualTo("November");
        await Assert.That(SageDue.MonthName(3)).IsEqualTo("März");
    }
}
