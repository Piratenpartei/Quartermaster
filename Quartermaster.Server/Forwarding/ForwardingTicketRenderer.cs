using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Forwarding.Redmine;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Forwarding;

/// <summary>Renders ticket subjects, descriptions, custom fields, outcome notes and failure mails from the forwarding templates.</summary>
public class ForwardingTicketRenderer {
    public const string ApplicationTemplate = "forwarding.redmine.application";
    public const string DueSelectionTemplate = "forwarding.redmine.due_selection";
    public const string ReductionTemplate = "forwarding.redmine.reduction";
    public const string OutcomeNoteTemplate = "forwarding.redmine.outcome_note";
    public const string FailureMailTemplate = "forwarding.failure.email";

    private const int MaxSubjectLength = 255;
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly TemplateRepository _templates;
    private readonly ChapterRepository _chapters;
    private readonly OptionRepository _options;

    public ForwardingTicketRenderer(TemplateRepository templates, ChapterRepository chapters, OptionRepository options) {
        _templates = templates;
        _chapters = chapters;
        _options = options;
    }

    /// <summary>
    /// The last line of every ticket description and the search term for adopting a ticket after an interrupted request. Later steps carry their number,
    /// so a chapter ticket in the same project as the federal one is never mistaken for it.
    /// </summary>
    public static string ReferenceLine(ForwardingCase forwardingCase, ForwardingStep step) {
        var line = $"Quartermaster-Vorgang {forwardingCase.Reference}";
        return step.Order > 1 ? $"{line} (Schritt {step.Order})" : line;
    }

    public static string TemplateFor(ForwardingPurpose purpose) {
        return purpose switch {
            ForwardingPurpose.Application => ApplicationTemplate,
            ForwardingPurpose.Reduction => ReductionTemplate,
            _ => DueSelectionTemplate
        };
    }

    public async Task<RenderedTicket> RenderTicketAsync(ForwardingCase forwardingCase, ForwardingStep step, ForwardingTarget target, int? federalTicketId, string? federalTicketUrl) {
        var template = Resolve(TemplateFor(step.Purpose), step.TargetChapterId);
        var model = BuildModel(forwardingCase, step.TargetChapterId, federalTicketId, federalTicketUrl);

        var subject = SingleLine(await RenderAsync(template.Subject ?? "{{ case.Kind }} {{ case.Reference }}", model));
        var description = (await RenderAsync(template.Body, model)).TrimEnd() + "\n\n" + ReferenceLine(forwardingCase, step);
        var customFields = await RenderCustomFieldsAsync(target.CustomFieldsJson, model);
        return new RenderedTicket(subject, description, customFields);
    }

    /// <summary>The note goes onto the federal ticket, so like that ticket it takes the template of the note step's (federal) chapter.</summary>
    public async Task<string> RenderNoteAsync(ForwardingCase forwardingCase, ForwardingStep step, ForwardingDecision decision) {
        var template = Resolve(OutcomeNoteTemplate, step.TargetChapterId);
        var model = BuildModel(forwardingCase, decision.ChapterId, null, null);
        model["outcome"] = new ForwardingOutcomeModel {
            Result = ResultLabel(decision.Result),
            DecidedBy = "Vorstand " + ChapterName(decision.ChapterId),
            Date = FormatDate(decision.DecidedAt),
            MotionUrl = BaseUrl() + decision.Path
        };
        return (await RenderAsync(template.Body, model)).TrimEnd();
    }

    public async Task<(string Subject, string Html)> RenderFailureMailAsync(ForwardingCase forwardingCase, ForwardingStep step, RenderedTicket? ticket) {
        var template = Resolve(FailureMailTemplate, step.TargetChapterId);
        var model = BuildModel(forwardingCase, step.TargetChapterId, null, null);
        model["failure"] = new ForwardingFailureModel {
            Error = step.LastError ?? "",
            Purpose = PurposeLabel(step.Purpose),
            TargetChapter = ChapterName(step.TargetChapterId),
            TicketSubject = ticket?.Subject ?? "",
            TicketDescription = ticket?.Description ?? "",
            CasesUrl = $"{BaseUrl()}/Administration/Forwarding"
        };

        var subject = SingleLine(await RenderAsync(template.Subject ?? "Weiterleitung fehlgeschlagen: {{ case.Reference }}", model));
        var (html, error) = await TemplateRenderer.RenderHtmlAsync(template.Body, model);
        if (error != null)
            throw new RedmineException($"Die Vorlage {FailureMailTemplate} ist fehlerhaft: {error}", RedmineException.Rejected);
        return (subject, html!);
    }

    private Template Resolve(string identifier, Guid chapterId) {
        return _templates.Resolve(identifier, chapterId, _chapters)
            ?? throw new RedmineException($"Die Vorlage {identifier} fehlt.", RedmineException.NotConfigured);
    }

    private static async Task<string> RenderAsync(string template, Dictionary<string, object> model) {
        var (text, error) = await TemplateRenderer.RenderTextAsync(template, model);
        if (error != null)
            throw new RedmineException($"Eine Vorlage der Weiterleitung ist fehlerhaft: {error}", RedmineException.Rejected);
        return text ?? "";
    }

    private static async Task<List<RedmineCustomFieldValue>> RenderCustomFieldsAsync(string json, Dictionary<string, object> model) {
        List<ForwardingCustomField>? fields;
        try {
            fields = JsonSerializer.Deserialize<List<ForwardingCustomField>>(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        } catch (JsonException ex) {
            throw new RedmineException("Die Feldzuordnung des Zielprojekts ist ungültig.", RedmineException.Rejected, inner: ex);
        }

        var values = new List<RedmineCustomFieldValue>();
        foreach (var field in fields ?? []) {
            var value = (await RenderAsync(field.Template ?? "", model)).Trim();
            if (value.Length > 0)
                values.Add(new RedmineCustomFieldValue(field.FieldId, value));
        }

        return values;
    }

    private Dictionary<string, object> BuildModel(ForwardingCase forwardingCase, Guid targetChapterId, int? federalTicketId, string? federalTicketUrl) {
        var payload = Payload(forwardingCase);
        var application = payload?.Application;
        var dues = payload?.DueSelection ?? application?.DueSelection;
        var memberFirstName = string.IsNullOrEmpty(dues?.FirstName) ? application?.FirstName ?? "" : dues.FirstName;
        var memberLastName = string.IsNullOrEmpty(dues?.LastName) ? application?.LastName ?? "" : dues.LastName;
        var memberEmail = string.IsNullOrEmpty(dues?.Email) ? application?.Email ?? "" : dues.Email;

        return new Dictionary<string, object> {
            ["application"] = application == null ? new ForwardingApplicationModel() : ApplicationModel(application, forwardingCase.ApplicationChapterId),
            ["dues"] = dues == null ? new ForwardingDuesModel() : DuesModel(dues),
            ["member"] = new ForwardingMemberModel {
                Number = dues != null && dues.MemberNumber != 0 ? dues.MemberNumber.ToString(CultureInfo.InvariantCulture) : "",
                FirstName = memberFirstName,
                LastName = memberLastName,
                Email = memberEmail
            },
            ["submission"] = new ForwardingSubmissionModel {
                SubmittedAt = payload == null ? "" : FormatDateTime(payload.SubmittedAt),
                SubmittedBy = payload?.SubmittedByName ?? "",
                EnteredByHand = payload?.EnteredByHand ?? false
            },
            ["chapter"] = new ForwardingChapterModel {
                Name = ChapterName(targetChapterId),
                DecidingName = forwardingCase.DecidingChapterId == null ? "" : ChapterName(forwardingCase.DecidingChapterId.Value),
                StateName = forwardingCase.StateChapterId == null ? "" : ChapterName(forwardingCase.StateChapterId.Value)
            },
            ["case"] = new ForwardingCaseModel {
                Reference = forwardingCase.Reference,
                RelatedReference = forwardingCase.RelatedCaseId == null ? "" : ForwardingCase.ReferenceFor(forwardingCase.RelatedCaseId.Value),
                Kind = forwardingCase.Kind == ForwardingCaseKind.Application ? "Mitgliedsantrag" : "Beitragseinstufung"
            },
            ["federalTicket"] = new ForwardingTicketModel {
                Id = federalTicketId?.ToString(CultureInfo.InvariantCulture) ?? "",
                Url = federalTicketUrl ?? ""
            },
            ["outcome"] = new ForwardingOutcomeModel(),
            ["failure"] = new ForwardingFailureModel()
        };
    }

    private ForwardingApplicationModel ApplicationModel(MembershipApplicationDTO application, Guid? chapterId) {
        return new ForwardingApplicationModel {
            FirstName = application.FirstName,
            LastName = application.LastName,
            DateOfBirth = application.DateOfBirth.ToString("dd.MM.yyyy", German),
            Citizenship = application.Citizenship,
            Email = application.Email,
            PhoneNumber = application.PhoneNumber,
            Street = application.AddressStreet,
            HouseNumber = application.AddressHouseNbr,
            PostCode = application.AddressPostCode,
            City = application.AddressCity,
            ChapterName = chapterId == null ? "" : ChapterName(chapterId.Value),
            EntryDate = application.EntryDate.ToString("dd.MM.yyyy", German),
            IsMemberOfAnotherParty = application.IsMemberOfAnotherParty,
            HasPriorDeclinedApplication = application.HasPriorDeclinedApplication,
            ApplicationText = application.ApplicationText
        };
    }

    private static ForwardingDuesModel DuesModel(DueSelectionDTO dues) {
        return new ForwardingDuesModel {
            Valuation = ValuationLabel(dues.SelectedValuation),
            YearlyAmount = FormatAmount(DueAmount.Effective(dues)),
            Reduced = dues.SelectedValuation == SelectedValuation.Reduced,
            ReducedAmount = FormatAmount(dues.ReducedAmount),
            Justification = dues.ReducedJustification,
            ReducedTimeSpan = dues.ReducedTimeSpan == ReducedTimeSpan.Permanent ? "dauerhaft" : "für ein Jahr",
            MonthlyIncomeGroup = FormatAmount(dues.MonthlyIncomeGroup),
            YearlyIncome = FormatAmount(dues.YearlyIncome),
            PaymentSchedule = ScheduleLabel(dues.PaymentSchedule),
            IsDirectDeposit = dues.IsDirectDeposit,
            AccountHolder = dues.AccountHolder,
            Iban = dues.IBAN
        };
    }

    private static ForwardingPayload? Payload(ForwardingCase forwardingCase) {
        return string.IsNullOrEmpty(forwardingCase.PayloadJson) ? null : JsonSerializer.Deserialize<ForwardingPayload>(forwardingCase.PayloadJson);
    }

    private string ChapterName(Guid chapterId) {
        return _chapters.Get(chapterId)?.Name ?? "";
    }

    private string BaseUrl() {
        return _options.GetGlobalValue(NotificationTemplateGlobals.BaseUrlOptionKey)?.Value?.Trim().TrimEnd('/') ?? "";
    }

    private static string SingleLine(string text) {
        var line = string.Join(" ", text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length > MaxSubjectLength ? line[..MaxSubjectLength] : line;
    }

    private static string FormatAmount(decimal amount) {
        return amount.ToString("N2", German);
    }

    private static string FormatDate(DateTime utc) {
        return BerlinTime.FromUtc(utc).ToString("dd.MM.yyyy", German);
    }

    private static string FormatDateTime(DateTime utc) {
        return BerlinTime.FromUtc(utc).ToString("dd.MM.yyyy HH:mm", German) + " Uhr";
    }

    private static string ValuationLabel(SelectedValuation valuation) {
        return valuation switch {
            SelectedValuation.MonthlyPayGroup => "nach monatlichem Einkommen",
            SelectedValuation.OnePercentYearlyPay => "1 % des Jahreseinkommens",
            SelectedValuation.Underage => "minderjährig",
            SelectedValuation.Reduced => "Beitragsminderung",
            _ => "keine Angabe"
        };
    }

    private static string ScheduleLabel(PaymentSchedule schedule) {
        return schedule switch {
            PaymentSchedule.Annual => "jährlich",
            PaymentSchedule.Quarterly => "vierteljährlich",
            PaymentSchedule.Monthly => "monatlich",
            _ => "keine Angabe"
        };
    }

    private static string PurposeLabel(ForwardingPurpose purpose) {
        return purpose switch {
            ForwardingPurpose.Application => "Mitgliedsantrag",
            ForwardingPurpose.Reduction => "Beitragsminderung",
            _ => "Beitragseinstufung"
        };
    }

    private static string ResultLabel(MotionApprovalStatus status) {
        return status switch {
            MotionApprovalStatus.Approved => "angenommen",
            MotionApprovalStatus.Rejected => "abgelehnt",
            MotionApprovalStatus.FormallyRejected => "formell abgelehnt",
            MotionApprovalStatus.ClosedWithoutAction => "ohne Beschluss geschlossen",
            _ => "offen"
        };
    }

    private sealed record ForwardingCustomField(int FieldId, string? Template);
}
