using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Templates;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>Renders the cover letters of shipments from the chapter's version of the cover letter template.</summary>
public class CoverLetterRenderer {
    public const string TemplateIdentifier = "templates.support_signatures.cover_letter";

    private readonly TemplateRepository _templates;
    private readonly ChapterRepository _chapters;
    private readonly DbContext _db;

    public CoverLetterRenderer(TemplateRepository templates, ChapterRepository chapters, DbContext db) {
        _templates = templates;
        _chapters = chapters;
        _db = db;
    }

    public async Task<(List<CoverLetter>? Letters, string? Error)> BuildAsync(SignatureCampaign campaign, IReadOnlyList<Guid> shipmentIds,
        CancellationToken ct) {
        var template = _templates.Resolve(TemplateIdentifier, campaign.ChapterId, _chapters);
        var chapter = _chapters.Get(campaign.ChapterId);
        if (template == null || chapter == null)
            return (null, "Die Vorlage für das Anschreiben fehlt.");

        var letters = new List<CoverLetter>();
        foreach (var model in Models(campaign, shipmentIds)) {
            ct.ThrowIfCancellationRequested();
            var (body, error, context) = await TemplateRenderer.RenderTextWithContextAsync(template.Body, new Dictionary<string, object> {
                ["letter"] = model,
                ["chapter"] = chapter.ToDto()
            });
            if (error != null)
                return (null, error);

            letters.Add(new CoverLetter(EnvelopeTags.Extract(context!), (body ?? "").Trim()));
        }

        return (letters, null);
    }

    public async Task<(byte[]? Pdf, string? Error)> RenderAsync(SignatureCampaign campaign, IReadOnlyList<Guid> shipmentIds, CancellationToken ct) {
        var (letters, error) = await BuildAsync(campaign, shipmentIds, ct);
        if (letters == null)
            return (null, error);

        return (TemplateEnvelopePdfRenderer.RenderMany(letters.Select(l => (l.Envelope, l.Body)).ToList()), null);
    }

    private List<SupportSignatureLetterModel> Models(SignatureCampaign campaign, IReadOnlyList<Guid> shipmentIds) {
        var ids = shipmentIds.Distinct().ToList();
        var shipments = _db.SignatureShipments
            .Where(s => ids.Contains(s.Id) && s.CampaignId == campaign.Id)
            .ToList();
        var lines = _db.SignatureShipmentLines
            .Where(l => ids.Contains(l.ShipmentId))
            .ToList();
        var officeIds = shipments.Select(s => s.OfficeId).Distinct().ToList();
        var offices = _db.CertificationOffices
            .Where(o => officeIds.Contains(o.Id))
            .ToDictionary(o => o.Id);
        var municipalityIds = lines.Select(l => l.AdministrativeDivisionId).Distinct().ToList();
        var municipalities = _db.AdministrativeDivisions
            .Where(d => municipalityIds.Contains(d.Id))
            .ToDictionary(d => d.Id, d => d.Name);
        var today = BerlinTime.FromUtc(DateTime.UtcNow).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return shipments
            .OrderBy(s => offices[s.OfficeId].Name)
            .Select(s => Model(campaign, offices[s.OfficeId], lines.Where(l => l.ShipmentId == s.Id).ToList(), municipalities,
                s.SentOn?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? today))
            .ToList();
    }

    private static SupportSignatureLetterModel Model(SignatureCampaign campaign, CertificationOffice office, List<SignatureShipmentLine> lines,
        Dictionary<Guid, string> municipalities, string date) {
        return new SupportSignatureLetterModel {
            Campaign = new SupportSignatureLetterCampaign { Name = campaign.Name, Deadline = LastDay(campaign.Deadline) },
            Office = new SupportSignatureLetterOffice {
                Name = office.Name,
                Addition = office.AddressAddition ?? "",
                Street = office.Street,
                PostCode = office.PostCode,
                City = office.City
            },
            Lines = lines
                .Select(l => new SupportSignatureLetterLine { Municipality = municipalities.GetValueOrDefault(l.AdministrativeDivisionId, ""), Count = l.Count })
                .OrderBy(l => l.Municipality)
                .ToList(),
            Total = lines.Sum(l => l.Count),
            Date = date
        };
    }

    /// <summary>Deadlines are stored as the instant they end, so a deadline at 00:00 names the day before as the last day.</summary>
    private static string LastDay(DateTime deadlineUtc) {
        return BerlinTime.FromUtc(deadlineUtc).AddSeconds(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
