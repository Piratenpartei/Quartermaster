using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Templates;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Data.Templates;
using Quartermaster.Server.SupportSignatures;
using Quartermaster.Server.Templates;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class CoverLetterRendererTests : RepositoryTestBase {
    private Chapter _chapter = default!;
    private SignatureCampaign _campaign = default!;
    private TemplateRepository _templates = default!;
    private CoverLetterRenderer _renderer = default!;

    [Before(Test)]
    public void Setup() {
        _templates = new TemplateRepository(Db);
        _templates.SupplementDefaults();
        _chapter = new Chapter { Name = "Landesverband Niedersachsen" };
        Db.Insert(_chapter);
        _campaign = new SignatureCampaign { ChapterId = _chapter.Id, Name = "Landesliste Landtagswahl 2027", Target = 2000, Deadline = new DateTime(2027, 6, 1), CreatedAt = DateTime.UtcNow };
        Db.Insert(_campaign);
        _renderer = new CoverLetterRenderer(_templates, new ChapterRepository(Db, AuditLog), Db);
    }

    private Guid Shipment(string officeName, string municipalityName, int count, string? addition = null) {
        var municipality = new AdministrativeDivision { Name = municipalityName, Depth = 7 };
        Db.Insert(municipality);
        var office = new CertificationOffice { Name = officeName, AddressAddition = addition, Street = "Am Markt 1", PostCode = "29331", City = "Lachendorf" };
        new CertificationOfficeRepository(Db).Create(office, [municipality.Id]);
        var shipment = new SignatureShipment { CampaignId = _campaign.Id, OfficeId = office.Id, CreatedAt = DateTime.UtcNow };
        new SignatureShipmentRepository(Db).Create(shipment, [new SignatureShipmentLine { AdministrativeDivisionId = municipality.Id, Count = count }]);
        return shipment.Id;
    }

    [Test]
    public async Task Address_addition_goes_under_the_name() {
        var shipment = Shipment("Stadt Salzgitter", "Salzgitter", 5, "Wahlbüro");

        var (letters, _) = await _renderer.BuildAsync(_campaign, [shipment], CancellationToken.None);

        await Assert.That(letters![0].Envelope.RecipientName).IsEqualTo("Stadt Salzgitter");
        await Assert.That(letters[0].Envelope.RecipientAddition).IsEqualTo("Wahlbüro");
    }

    [Test]
    public async Task Renders_one_letter_per_shipment() {
        var first = Shipment("Samtgemeinde Lachendorf", "Ahnsbeck", 12);
        var second = Shipment("Stadt Celle", "Celle", 3);

        var (letters, error) = await _renderer.BuildAsync(_campaign, [first, second], CancellationToken.None);

        await Assert.That(error).IsNull();
        await Assert.That(letters!.Count).IsEqualTo(2);
        await Assert.That(letters.Select(l => l.Envelope.RecipientName).ToList()).IsEquivalentTo(["Samtgemeinde Lachendorf", "Stadt Celle"]);
        await Assert.That(letters[0].Envelope.SenderName).IsEqualTo("Landesverband Niedersachsen");
        await Assert.That(letters.Single(l => l.Envelope.RecipientName == "Samtgemeinde Lachendorf").Body).Contains("Ahnsbeck: 12");
        await Assert.That(letters[0].Body).Contains("Landesliste Landtagswahl 2027");
    }

    [Test]
    public async Task Sent_shipment_letter_is_dated_with_its_sent_day() {
        var shipment = Shipment("Samtgemeinde Lachendorf", "Ahnsbeck", 12);
        new SignatureShipmentRepository(Db).SetSentOn(shipment, new DateTime(2027, 2, 3));

        var (letters, _) = await _renderer.BuildAsync(_campaign, [shipment], CancellationToken.None);

        await Assert.That(letters!.Single().Body).Contains("03.02.2027");
    }

    [Test]
    public async Task Uses_the_chapter_override() {
        var shipment = Shipment("Samtgemeinde Lachendorf", "Ahnsbeck", 12);
        var baseTemplate = _templates.GetSystemBase(CoverLetterRenderer.TemplateIdentifier)!;
        _templates.Create(new Template {
            Identifier = CoverLetterRenderer.TemplateIdentifier,
            DisplayName = baseTemplate.DisplayName,
            ChapterId = _chapter.Id,
            BaseTemplateId = baseTemplate.Id,
            Body = "Eigenes Anschreiben an {{ letter.Office.Name }}",
            RenderMode = TemplateRenderMode.Envelope,
            CreatedAt = DateTime.UtcNow
        });

        var (letters, _) = await _renderer.BuildAsync(_campaign, [shipment], CancellationToken.None);

        await Assert.That(letters!.Single().Body).IsEqualTo("Eigenes Anschreiben an Samtgemeinde Lachendorf");
    }

    [Test]
    public async Task Renders_a_pdf_with_all_letters() {
        var first = Shipment("Samtgemeinde Lachendorf", "Ahnsbeck", 12);
        var second = Shipment("Stadt Celle", "Celle", 3);

        var (pdf, error) = await _renderer.RenderAsync(_campaign, [first, second], CancellationToken.None);

        await Assert.That(error).IsNull();
        await Assert.That(Encoding.ASCII.GetString(pdf!, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task Seeded_template_is_an_envelope_letter_and_renders_with_mock_data() {
        var template = _templates.GetSystemBase(CoverLetterRenderer.TemplateIdentifier)!;

        var (pdf, error) = await TemplatePdfRenderer.RenderAsync(template);

        await Assert.That(template.RenderMode).IsEqualTo(TemplateRenderMode.Envelope);
        await Assert.That(error).IsNull();
        await Assert.That(pdf).IsNotNull();
    }
}
