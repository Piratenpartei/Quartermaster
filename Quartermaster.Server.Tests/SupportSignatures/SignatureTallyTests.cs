using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class SignatureTallyTests : RepositoryTestBase {
    private static readonly Guid Municipality = Guid.NewGuid();

    private static SignatureIntake Intake(int count, bool alreadyCertified = false, Guid? municipality = null) {
        return new SignatureIntake { AdministrativeDivisionId = municipality ?? Municipality, Count = count, AlreadyCertified = alreadyCertified };
    }

    [Test]
    public async Task Waiting_and_open_follow_the_spec() {
        var tallies = SignatureTally.PerMunicipality(
            [Intake(10), Intake(3, alreadyCertified: true)],
            [new SignatureShipmentLine { AdministrativeDivisionId = Municipality, Count = 8 }],
            [],
            [new SignatureReturnLine { AdministrativeDivisionId = Municipality, Certified = 5, Rejected = 1 }]);

        var tally = tallies[Municipality];
        await Assert.That(tally.Received).IsEqualTo(13);
        await Assert.That(tally.AlreadyCertified).IsEqualTo(3);
        await Assert.That(tally.Waiting).IsEqualTo(2);
        await Assert.That(tally.Open).IsEqualTo(2);
        await Assert.That(tally.CertifiedTotal).IsEqualTo(8);
    }

    [Test]
    public async Task Prepared_forms_stay_waiting_but_are_not_plannable() {
        var tallies = SignatureTally.PerMunicipality(
            [Intake(10)],
            [],
            [new SignatureShipmentLine { AdministrativeDivisionId = Municipality, Count = 6 }],
            []);

        await Assert.That(tallies[Municipality].Waiting).IsEqualTo(10);
        await Assert.That(tallies[Municipality].Plannable).IsEqualTo(4);
    }

    [Test]
    public async Task Load_counts_only_confirmed_shipments_as_sent() {
        var chapter = new Chapter { Name = "Niedersachsen" };
        Db.Insert(chapter);
        var division = new AdministrativeDivision { Name = "Ahnsbeck", Depth = 7 };
        Db.Insert(division);
        var office = new CertificationOffice { Name = "Samtgemeinde Lachendorf" };
        Db.Insert(office);
        var campaign = new SignatureCampaign { ChapterId = chapter.Id, Name = "Landesliste", Target = 2000, Deadline = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow };
        Db.Insert(campaign);
        Db.Insert(new SignatureIntake { CampaignId = campaign.Id, AdministrativeDivisionId = division.Id, Count = 10, ReceivedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow });
        var sent = new SignatureShipment { CampaignId = campaign.Id, OfficeId = office.Id, SentOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow };
        var prepared = new SignatureShipment { CampaignId = campaign.Id, OfficeId = office.Id, CreatedAt = DateTime.UtcNow };
        Db.Insert(sent);
        Db.Insert(prepared);
        Db.Insert(new SignatureShipmentLine { ShipmentId = sent.Id, AdministrativeDivisionId = division.Id, Count = 4 });
        Db.Insert(new SignatureShipmentLine { ShipmentId = prepared.Id, AdministrativeDivisionId = division.Id, Count = 3 });

        var tally = SignatureTally.Load(Db, campaign.Id)[division.Id];

        await Assert.That(tally.Sent).IsEqualTo(4);
        await Assert.That(tally.Prepared).IsEqualTo(3);
        await Assert.That(tally.Waiting).IsEqualTo(6);
        await Assert.That(tally.Plannable).IsEqualTo(3);
    }

    [Test]
    [Arguments(2000, 20, 2400)]
    [Arguments(100, 15, 115)]
    [Arguments(7, 10, 8)]
    public async Task Goal_rounds_up(int target, int margin, int goal) {
        await Assert.That(SignatureTally.Goal(target, margin)).IsEqualTo(goal);
    }

    [Test]
    public async Task Totals_sum_all_municipalities() {
        var other = Guid.NewGuid();
        var tallies = SignatureTally.PerMunicipality(
            [Intake(10), Intake(5, municipality: other), Intake(2, alreadyCertified: true, municipality: other)],
            [new SignatureShipmentLine { AdministrativeDivisionId = Municipality, Count = 10 }],
            [],
            [new SignatureReturnLine { AdministrativeDivisionId = Municipality, Certified = 7, Rejected = 2 }]);

        var totals = SignatureTally.Totals(tallies, 100, 20);

        await Assert.That(totals.Received).IsEqualTo(17);
        await Assert.That(totals.Waiting).IsEqualTo(5);
        await Assert.That(totals.AtOffices).IsEqualTo(1);
        await Assert.That(totals.Certified).IsEqualTo(9);
        await Assert.That(totals.Rejected).IsEqualTo(2);
        await Assert.That(totals.Goal).IsEqualTo(120);
    }
}
