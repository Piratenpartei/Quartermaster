using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Server.SupportSignatures;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class ShipmentPlannerTests {
    private static readonly Guid Ahnsbeck = Guid.NewGuid();
    private static readonly Guid Eldingen = Guid.NewGuid();
    private static readonly Guid Celle = Guid.NewGuid();
    private static readonly Guid Lachendorf = Guid.NewGuid();
    private static readonly Guid CelleOffice = Guid.NewGuid();

    private static MunicipalityTally Waiting(int count) {
        return new MunicipalityTally(count, 0, 0, 0, 0, 0);
    }

    [Test]
    public async Task Groups_waiting_forms_by_office() {
        var tallies = new Dictionary<Guid, MunicipalityTally> { [Ahnsbeck] = Waiting(3), [Eldingen] = Waiting(2), [Celle] = Waiting(5) };
        var offices = new Dictionary<Guid, Guid> { [Ahnsbeck] = Lachendorf, [Eldingen] = Lachendorf, [Celle] = CelleOffice };

        var plan = ShipmentPlanner.Plan(tallies, offices);

        var lachendorf = plan.Offices.Single(o => o.OfficeId == Lachendorf);
        await Assert.That(plan.Offices.Count).IsEqualTo(2);
        await Assert.That(lachendorf.Lines.Sum(l => l.Count)).IsEqualTo(5);
        await Assert.That(lachendorf.Lines.Select(l => l.MunicipalityId).ToList()).IsEquivalentTo([Ahnsbeck, Eldingen]);
        await Assert.That(plan.Unassigned.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Municipalities_without_office_are_unassigned() {
        var tallies = new Dictionary<Guid, MunicipalityTally> { [Ahnsbeck] = Waiting(3) };

        var plan = ShipmentPlanner.Plan(tallies, new Dictionary<Guid, Guid>());

        await Assert.That(plan.Offices.Count).IsEqualTo(0);
        await Assert.That(plan.Unassigned.Single().Count).IsEqualTo(3);
    }

    [Test]
    public async Task Nothing_plannable_gives_an_empty_plan() {
        var tallies = new Dictionary<Guid, MunicipalityTally> {
            [Ahnsbeck] = new MunicipalityTally(5, 0, 5, 0, 0, 0),
            [Eldingen] = new MunicipalityTally(4, 0, 0, 4, 0, 0),
            [Celle] = new MunicipalityTally(2, 2, 0, 0, 0, 0)
        };
        var offices = new Dictionary<Guid, Guid> { [Ahnsbeck] = Lachendorf, [Eldingen] = Lachendorf, [Celle] = CelleOffice };

        var plan = ShipmentPlanner.Plan(tallies, offices);

        await Assert.That(plan.Offices.Count).IsEqualTo(0);
        await Assert.That(plan.Unassigned.Count).IsEqualTo(0);
    }
}
