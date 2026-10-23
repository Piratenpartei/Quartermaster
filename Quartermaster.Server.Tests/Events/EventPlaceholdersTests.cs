using System;
using System.Threading.Tasks;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Tests.Events;

public class EventPlaceholdersTests {
    [Test]
    public async Task Fills_date_time_and_link_in_both_languages() {
        var text = "Am {{datum}} um {{uhrzeit}} Uhr ({{date}}, {{time}}), online unter {{link}}";

        var result = EventPlaceholders.Apply(text, new DateOnly(2026, 11, 28), new TimeOnly(10, 0), "https://meet.jit.si/obpt");

        await Assert.That(result).IsEqualTo("Am 28.11.2026 um 10:00 Uhr (28.11.2026, 10:00), online unter https://meet.jit.si/obpt");
    }

    [Test]
    public async Task Missing_values_become_empty_or_the_date_fallback() {
        var result = EventPlaceholders.Apply("{{datum}}|{{uhrzeit}}|{{link}}", null, null, null, "noch offen");

        await Assert.That(result).IsEqualTo("noch offen||");
    }
}
