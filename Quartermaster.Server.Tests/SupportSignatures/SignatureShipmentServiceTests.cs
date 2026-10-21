using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class SignatureShipmentServiceTests : RepositoryTestBase {
    private static readonly Guid UserId = Guid.NewGuid();
    private SignatureCampaign _campaign = default!;
    private AdministrativeDivision _ahnsbeck = default!;
    private AdministrativeDivision _celle = default!;
    private CertificationOffice _lachendorf = default!;
    private CertificationOffice _celleOffice = default!;
    private SignatureShipmentService _service = default!;

    [Before(Test)]
    public void Setup() {
        var chapter = new Chapter { Name = "Niedersachsen" };
        Db.Insert(chapter);
        _ahnsbeck = new AdministrativeDivision { Name = "Ahnsbeck", Depth = 7 };
        _celle = new AdministrativeDivision { Name = "Celle", Depth = 7 };
        Db.Insert(_ahnsbeck);
        Db.Insert(_celle);
        var offices = new CertificationOfficeRepository(Db);
        _lachendorf = new CertificationOffice { Name = "Samtgemeinde Lachendorf", Street = "Oppershäuser Str. 1", PostCode = "29331", City = "Lachendorf" };
        _celleOffice = new CertificationOffice { Name = "Stadt Celle", Street = "Am Markt 14", PostCode = "29221", City = "Celle" };
        offices.Create(_lachendorf, [_ahnsbeck.Id]);
        offices.Create(_celleOffice, [_celle.Id]);
        _campaign = new SignatureCampaign { ChapterId = chapter.Id, Name = "Landesliste", Target = 2000, Deadline = DateTime.UtcNow.AddDays(30), OverdueDays = 21, CreatedAt = DateTime.UtcNow };
        Db.Insert(_campaign);
        Intake(_ahnsbeck, 10);
        Intake(_celle, 4);
        _service = NewServiceOn(Db);
    }

    private static SignatureShipmentService NewServiceOn(DbContext db) {
        var auditLog = new AuditLogRepository(db);
        return new SignatureShipmentService(new SignatureShipmentRepository(db), new SignatureCampaignRepository(db), new CertificationOfficeRepository(db), auditLog, db);
    }

    private void Intake(AdministrativeDivision municipality, int count) {
        Db.Insert(new SignatureIntake { CampaignId = _campaign.Id, AdministrativeDivisionId = municipality.Id, Count = count, ReceivedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow });
    }

    private Guid PrepareAndConfirm(CertificationOffice office, DateOnly sentOn) {
        var (ids, _) = _service.Prepare(_campaign.Id, [office.Id], UserId);
        _service.Confirm(ids.Single(), sentOn);
        return ids.Single();
    }

    private static ReturnLineInput Line(AdministrativeDivision municipality, int certified, int rejected = 0, SignatureRejectionReason? reason = null) {
        return new ReturnLineInput { MunicipalityId = municipality.Id, Certified = certified, Rejected = rejected, RejectionReason = reason };
    }

    [Test]
    public async Task Prepare_creates_one_shipment_per_selected_office() {
        var (ids, outcome) = _service.Prepare(_campaign.Id, [_lachendorf.Id, _celleOffice.Id], UserId);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(ids.Count).IsEqualTo(2);
        await Assert.That(Db.SignatureShipments.Count(s => s.SentOn == null)).IsEqualTo(2);
        await Assert.That(Db.SignatureShipmentLines.Sum(l => l.Count)).IsEqualTo(14);
    }

    [Test]
    public async Task Second_prepare_finds_nothing() {
        _service.Prepare(_campaign.Id, [_lachendorf.Id], UserId);

        var (ids, _) = _service.Prepare(_campaign.Id, [_lachendorf.Id], UserId);

        await Assert.That(ids.Count).IsEqualTo(0);
        await Assert.That(Db.SignatureShipments.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Parallel_prepares_ship_the_forms_once() {
        using var dbA = Worker.CreateDbContext();
        using var dbB = Worker.CreateDbContext();
        var serviceA = NewServiceOn(dbA);
        var serviceB = NewServiceOn(dbB);

        var taskA = Task.Run(() => serviceA.Prepare(_campaign.Id, [_lachendorf.Id], UserId).Ids.Count);
        var taskB = Task.Run(() => serviceB.Prepare(_campaign.Id, [_lachendorf.Id], UserId).Ids.Count);
        var created = await Task.WhenAll(taskA, taskB);

        await Assert.That(created.Sum()).IsEqualTo(1);
        await Assert.That(Db.SignatureShipmentLines.Sum(l => l.Count)).IsEqualTo(10);
    }

    [Test]
    public async Task Confirming_a_shipment_discarded_meanwhile_is_not_found() {
        var (ids, _) = _service.Prepare(_campaign.Id, [_lachendorf.Id], UserId);
        using var holder = Worker.CreateDbContext();
        using var dbA = Worker.CreateDbContext();
        var holderCampaigns = new SignatureCampaignRepository(holder);
        var serviceA = NewServiceOn(dbA);

        SupportSignatureOutcome outcome;
        using (var tx = holderCampaigns.BeginTransaction()) {
            holderCampaigns.Lock(_campaign.Id);
            var confirm = Task.Run(() => serviceA.Confirm(ids.Single(), DateOnly.FromDateTime(DateTime.UtcNow)));
            await Task.Delay(500);
            new SignatureShipmentRepository(holder).Delete(ids.Single());
            tx.Commit();
            outcome = await confirm;
        }

        await Assert.That(outcome.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Deleting_a_stale_intake_checks_its_current_count() {
        var intake = Db.SignatureIntakes.First(i => i.AdministrativeDivisionId == _ahnsbeck.Id);
        var stale = new SignatureIntake { Id = intake.Id, CampaignId = intake.CampaignId, AdministrativeDivisionId = intake.AdministrativeDivisionId, Count = 1 };
        _service.CreateSingle(_campaign.Id, _lachendorf.Id, [new PlannedLine(_ahnsbeck.Id, 5)], UserId);
        var intakes = new SignatureIntakeService(new SignatureIntakeRepository(Db), new SignatureCampaignRepository(Db), new AdministrativeDivisionRepository(Db),
            new AuditLogRepository(Db), Db);

        var outcome = intakes.Delete(stale);

        await Assert.That(outcome.StatusCode).IsEqualTo(409);
        await Assert.That(Db.SignatureIntakes.Any(i => i.Id == intake.Id)).IsTrue();
    }

    [Test]
    public async Task Single_shipment_cannot_exceed_waiting() {
        var outcome = _service.CreateSingle(_campaign.Id, _lachendorf.Id, [new PlannedLine(_ahnsbeck.Id, 11)], UserId).Outcome;

        await Assert.That(outcome.StatusCode).IsEqualTo(409);
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.Shipment.ExceedsWaiting);
    }

    [Test]
    public async Task Single_shipment_only_takes_municipalities_of_its_office() {
        var outcome = _service.CreateSingle(_campaign.Id, _lachendorf.Id, [new PlannedLine(_celle.Id, 1)], UserId).Outcome;

        await Assert.That(outcome.StatusCode).IsEqualTo(400);
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.Shipment.WrongOffice);
    }

    [Test]
    public async Task Discard_only_prepared() {
        var sent = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        var (prepared, _) = _service.Prepare(_campaign.Id, [_celleOffice.Id], UserId);

        var discardSent = _service.Discard(sent);
        var discardPrepared = _service.Discard(prepared.Single());

        await Assert.That(discardSent.StatusCode).IsEqualTo(409);
        await Assert.That(discardPrepared.Succeeded).IsTrue();
        await Assert.That(Db.SignatureShipments.Count()).IsEqualTo(1);
        await Assert.That(Db.SignatureShipmentLines.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Confirm_twice_is_refused() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));

        var again = _service.Confirm(shipmentId, DateOnly.FromDateTime(DateTime.UtcNow));

        await Assert.That(again.StatusCode).IsEqualTo(409);
    }

    [Test]
    public async Task Partial_returns_cannot_exceed_the_line() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var first = _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 5, 1, SignatureRejectionReason.NotResident)], UserId);
        var tooMany = _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 5)], UserId);
        var rest = _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 4)], UserId);

        await Assert.That(first.Succeeded).IsTrue();
        await Assert.That(tooMany.StatusCode).IsEqualTo(409);
        await Assert.That(tooMany.ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.Return.ExceedsOpen);
        await Assert.That(rest.Succeeded).IsTrue();
        await Assert.That(SignatureTally.Load(Db, _campaign.Id)[_ahnsbeck.Id].Open).IsEqualTo(0);
    }

    [Test]
    public async Task Return_needs_a_sent_shipment_and_its_municipalities() {
        var (prepared, _) = _service.Prepare(_campaign.Id, [_lachendorf.Id], UserId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var onPrepared = _service.RecordReturn(prepared.Single(), today, [Line(_ahnsbeck, 1)], UserId);
        _service.Confirm(prepared.Single(), today);
        var foreignMunicipality = _service.RecordReturn(prepared.Single(), today, [Line(_celle, 1)], UserId);

        await Assert.That(onPrepared.StatusCode).IsEqualTo(409);
        await Assert.That(foreignMunicipality.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Reason_is_kept_with_rejections_and_dropped_without() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 2, 0, SignatureRejectionReason.SignedTwice), Line(_ahnsbeck, 0, 1, SignatureRejectionReason.SignedTwice), Line(_ahnsbeck, 0, 1)], UserId);

        var lines = Db.SignatureReturnLines.OrderBy(l => l.Rejected).ToList();
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0].RejectionReason).IsNull();
        await Assert.That(lines.Count(l => l.RejectionReason == SignatureRejectionReason.SignedTwice)).IsEqualTo(1);
    }

    [Test]
    public async Task Deleting_a_return_reopens_its_forms() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        _service.RecordReturn(shipmentId, DateOnly.FromDateTime(DateTime.UtcNow), [Line(_ahnsbeck, 10)], UserId);
        var returnId = Db.SignatureReturns.Single().Id;

        var outcome = _service.DeleteReturn(returnId);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(SignatureTally.Load(Db, _campaign.Id)[_ahnsbeck.Id].Open).IsEqualTo(10);
    }

    [Test]
    public async Task Overdue_after_overdue_days_with_open_forms() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var old = PrepareAndConfirm(_lachendorf, today.AddDays(-22));
        var recent = PrepareAndConfirm(_celleOffice, today.AddDays(-5));
        var shipments = _service.List(_campaign.Id, today);

        await Assert.That(shipments.Single(s => s.Id == old).IsOverdue).IsTrue();
        await Assert.That(shipments.Single(s => s.Id == recent).IsOverdue).IsFalse();

        _service.RecordReturn(old, today, [Line(_ahnsbeck, 10)], UserId);
        await Assert.That(_service.List(_campaign.Id, today).Single(s => s.Id == old).IsOverdue).IsFalse();
    }

    [Test]
    public async Task Updating_a_return_replaces_its_date_and_lines() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        _service.RecordReturn(shipmentId, new DateOnly(2027, 3, 1), [Line(_ahnsbeck, 4, 1, SignatureRejectionReason.NotResident)], UserId);
        var returnId = Db.SignatureReturns.Single().Id;

        var outcome = _service.UpdateReturn(returnId, new DateOnly(2027, 3, 2),
            [Line(_ahnsbeck, 8), Line(_ahnsbeck, 0, 1, SignatureRejectionReason.SignedTwice), Line(_ahnsbeck, 0, 1, SignatureRejectionReason.NotResident)]);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(Db.SignatureReturns.Single().ReturnedOn).IsEqualTo(new DateTime(2027, 3, 2));
        await Assert.That(Db.SignatureReturnLines.Count(l => l.ReturnId == returnId)).IsEqualTo(3);
        await Assert.That(SignatureTally.Load(Db, _campaign.Id)[_ahnsbeck.Id].Open).IsEqualTo(0);
    }

    [Test]
    public async Task Updating_a_return_cannot_exceed_the_line() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 4)], UserId);
        _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 3)], UserId);
        var firstId = Db.SignatureReturns.OrderBy(r => r.CreatedAt).First().Id;

        var outcome = _service.UpdateReturn(firstId, today, [Line(_ahnsbeck, 8)]);

        await Assert.That(outcome.StatusCode).IsEqualTo(409);
        await Assert.That(Db.SignatureReturnLines.Where(l => l.ReturnId == firstId).Sum(l => l.Certified)).IsEqualTo(4);
    }

    [Test]
    public async Task Listed_returns_carry_their_lines() {
        var shipmentId = PrepareAndConfirm(_lachendorf, DateOnly.FromDateTime(DateTime.UtcNow));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _service.RecordReturn(shipmentId, today, [Line(_ahnsbeck, 4), Line(_ahnsbeck, 0, 2, SignatureRejectionReason.SignedTwice)], UserId);

        var listed = _service.List(_campaign.Id, today).Single().Returns.Single();

        await Assert.That(listed.Lines.Count).IsEqualTo(2);
        await Assert.That(listed.Lines.Single(l => l.Rejected == 2).RejectionReason).IsEqualTo(SignatureRejectionReason.SignedTwice);
    }

    [Test]
    public async Task Overdue_counts_match_the_listed_shipments() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        PrepareAndConfirm(_lachendorf, today.AddDays(-22));
        PrepareAndConfirm(_celleOffice, today.AddDays(-5));

        var counts = _service.OverdueCounts([_campaign], today);

        await Assert.That(counts[_campaign.Id]).IsEqualTo(1);
    }
}
