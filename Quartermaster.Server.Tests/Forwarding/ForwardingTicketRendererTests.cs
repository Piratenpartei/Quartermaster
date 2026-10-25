using System.Text.Json;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.Redmine;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class ForwardingTicketRendererTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private TemplateRepository _templates = default!;
    private ForwardingTicketRenderer _renderer = default!;
    private Chapter _federal = default!;
    private Chapter _state = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _templates = new TemplateRepository(Db);
        _templates.SupplementDefaults();
        var options = new OptionRepository(Db, AuditLog);
        options.SupplementDefaults();
        options.SetValue("system.public_base_url", null, "https://qm.test");
        _renderer = new ForwardingTicketRenderer(_templates, new ChapterRepository(Db, AuditLog), options);
        _federal = _builder.SeedChapter("Piratenpartei Deutschland " + Guid.NewGuid().ToString("N")[..4]);
        _state = _builder.SeedChapter("Piratenpartei Niedersachsen", parentChapterId: _federal.Id);
    }

    private static MembershipApplicationDTO Application() => new() {
        FirstName = "Anna",
        LastName = "Pirat",
        DateOfBirth = new DateOnly(1990, 5, 1),
        Citizenship = "deutsch",
        Email = "anna@example.org",
        PhoneNumber = "0170 1",
        AddressStreet = "Hafenstraße",
        AddressHouseNbr = "7",
        AddressPostCode = "30159",
        AddressCity = "Hannover",
        EntryDate = new DateOnly(2026, 11, 1),
        DueSelection = new DueSelectionDTO {
            SelectedValuation = SelectedValuation.MonthlyPayGroup,
            SelectedDue = 120,
            IsDirectDeposit = true,
            AccountHolder = "Anna Pirat",
            IBAN = "DE02120300000000202051",
            PaymentSchedule = PaymentSchedule.Annual
        }
    };

    private (ForwardingCase Case, ForwardingStep Step) Case(ForwardingPurpose purpose, Guid target, MembershipApplicationDTO? application = null, DueSelectionDTO? due = null) {
        var payload = new ForwardingPayload(application, due, null, null, false, new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc));
        var forwardingCase = new ForwardingCase {
            Kind = application != null ? ForwardingCaseKind.Application : ForwardingCaseKind.DueSelection,
            ApplicationChapterId = _state.Id,
            DecidingChapterId = _state.Id,
            StateChapterId = _state.Id,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        };
        var step = new ForwardingStep { CaseId = forwardingCase.Id, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = target, Purpose = purpose, Order = 1 };
        return (forwardingCase, step);
    }

    private static ForwardingTarget Target(string customFieldsJson = "[]") => new() { ProjectId = 1, TrackerId = 2, CustomFieldsJson = customFieldsJson };

    [Test]
    public async Task Application_ticket_contains_all_fields() {
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, Application());

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), null, null);

        await Assert.That(ticket.Subject).Contains("Anna Pirat");
        foreach (var expected in new[] { "Anna", "Pirat", "01.05.1990", "deutsch", "anna@example.org", "0170 1", "Hafenstraße 7", "30159 Hannover", "01.11.2026", "120,00", "DE02120300000000202051", "Anna Pirat" }) {
            await Assert.That(ticket.Description).Contains(expected);
        }
    }

    [Test]
    public async Task Application_ticket_names_country_and_other_parties() {
        var application = Application();
        application.AddressCountry = "Österreich";
        application.IsMemberOfAnotherParty = true;
        application.OtherParties = "Volt";
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, application);

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), null, null);

        await Assert.That(ticket.Description).Contains("30159 Hannover, Österreich");
        await Assert.That(ticket.Description).Contains("Mitglied einer anderen Partei: ja (Volt)");
    }

    [Test]
    public async Task Address_in_germany_has_no_country() {
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, Application());

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), null, null);

        await Assert.That(ticket.Description).Contains("30159 Hannover\n");
    }

    [Test]
    public async Task Reduction_ticket_names_the_due_if_rejected() {
        var due = new DueSelectionDTO { MemberNumber = 4711, SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24, ReducedJustification = "Studium", SelectedDue = 120 };
        var (forwardingCase, step) = Case(ForwardingPurpose.Reduction, _state.Id, due: due);

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), null, null);

        await Assert.That(ticket.Description).Contains("Jahresbeitrag bei Ablehnung: 120,00 €");
    }

    [Test]
    public async Task Reference_line_is_appended() {
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, Application());

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), null, null);

        await Assert.That(ticket.Description).EndsWith("Quartermaster-Vorgang " + forwardingCase.Reference);
    }

    [Test]
    public async Task Chapter_override_template_wins() {
        var baseTemplate = _templates.Resolve("forwarding.redmine.application", null, new ChapterRepository(Db, AuditLog))!;
        _templates.Create(new Template {
            Identifier = baseTemplate.Identifier,
            DisplayName = baseTemplate.DisplayName,
            ChapterId = _state.Id,
            BaseTemplateId = baseTemplate.Id,
            Subject = "Land: {{ application.LastName }}",
            Body = "Nur Name: {{ application.FirstName }}"
        });
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _state.Id, Application());

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), null, null);

        await Assert.That(ticket.Subject).IsEqualTo("Land: Pirat");
        await Assert.That(ticket.Description).StartsWith("Nur Name: Anna");
    }

    [Test]
    public async Task Custom_fields_render_and_skip_empty() {
        var due = new DueSelectionDTO { MemberNumber = 4711, FirstName = "Anna", LastName = "Pirat", SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24, ReducedJustification = "Studium" };
        var (forwardingCase, step) = Case(ForwardingPurpose.Reduction, _federal.Id, due: due);
        var json = """[{"FieldId":5,"Template":"{{ member.Number }}"},{"FieldId":6,"Template":"{{ application.FirstName }}"}]""";

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(json), null, null);

        await Assert.That(ticket.CustomFields).IsEquivalentTo(new[] { new RedmineCustomFieldValue(5, "4711") });
    }

    [Test]
    public async Task Invalid_custom_field_json_is_rejected() {
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, Application());

        RedmineException? caught = null;
        try {
            await _renderer.RenderTicketAsync(forwardingCase, step, Target("{nope"), null, null);
        } catch (RedmineException ex) {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Code).IsEqualTo(RedmineException.Rejected);
    }

    [Test]
    public async Task Chapter_ticket_links_federal_ticket() {
        var due = new DueSelectionDTO { MemberNumber = 4711, SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24, ReducedJustification = "Studium" };
        var (forwardingCase, step) = Case(ForwardingPurpose.Reduction, _state.Id, due: due);

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target(), 815, "https://redmine.bund.invalid/issues/815");

        await Assert.That(ticket.Description).Contains("https://redmine.bund.invalid/issues/815");
        await Assert.That(ticket.Description).Contains("24,00");
        await Assert.That(ticket.Description).Contains("Studium");
    }

    [Test]
    public async Task Every_seed_renders_with_mock_data() {
        foreach (var identifier in new[] { "forwarding.redmine.application", "forwarding.redmine.due_selection", "forwarding.redmine.reduction", "forwarding.redmine.outcome_note", "forwarding.failure.email" }) {
            var template = _templates.Resolve(identifier, null, new ChapterRepository(Db, AuditLog));
            await Assert.That(template).IsNotNull();
            var mock = TemplateMockDataProvider.GetMockData(TemplateModelLookup.BuildForTemplate(identifier, false, false, false));
            var (body, bodyError) = await TemplateRenderer.RenderTextAsync(template!.Body, mock);
            await Assert.That(bodyError).IsNull();
            await Assert.That(body).DoesNotContain("{{");
            if (template.Subject != null) {
                var (_, subjectError) = await TemplateRenderer.RenderTextAsync(template.Subject, mock);
                await Assert.That(subjectError).IsNull();
            }
        }
    }
    [Test]
    public async Task Negative_member_number_is_kept() {
        var due = new DueSelectionDTO { MemberNumber = -42, SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24, ReducedJustification = "Studium" };
        var (forwardingCase, step) = Case(ForwardingPurpose.Reduction, _federal.Id, due: due);

        var ticket = await _renderer.RenderTicketAsync(forwardingCase, step, Target("""[{"FieldId":5,"Template":"{{ member.Number }}"}]"""), null, null);

        await Assert.That(ticket.Description).Contains("Mitgliedsnummer: -42");
        await Assert.That(ticket.CustomFields.Single().Value).IsEqualTo("-42");
    }
    [Test]
    public async Task Outcome_note_template_comes_from_the_federal_level() {
        var baseTemplate = _templates.Resolve("forwarding.redmine.outcome_note", null, new ChapterRepository(Db, AuditLog))!;
        _templates.Create(new Template {
            Identifier = baseTemplate.Identifier,
            DisplayName = baseTemplate.DisplayName,
            ChapterId = _state.Id,
            BaseTemplateId = baseTemplate.Id,
            Body = "Landesnotiz"
        });
        var (forwardingCase, _) = Case(ForwardingPurpose.Reduction, _federal.Id, due: new DueSelectionDTO { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24 });
        var note = new ForwardingStep { CaseId = forwardingCase.Id, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Reduction, Order = 2 };
        var decision = new ForwardingDecision(MotionApprovalStatus.Approved, _state.Id, new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc), "/Administration/DueSelections/x");

        var text = await _renderer.RenderNoteAsync(forwardingCase, note, decision);

        await Assert.That(text).DoesNotContain("Landesnotiz");
        await Assert.That(text).Contains("angenommen");
        await Assert.That(text).Contains("https://qm.test/Administration/DueSelections/x");
    }

    private static ForwardingSageModel Sage(string reductionResult = "", string partialIfReduced = "", string doneStatus = "", string doneAccount = "") {
        return new ForwardingSageModel {
            ProvisionalMemberNumber = "RM1001",
            EntryDate = "01.11.2026",
            EntryYear = "2026",
            EntryMonth = "November",
            YearlyAmount = "120",
            PartialAmount = "20",
            ReductionResult = reductionResult,
            PartialAmountIfReduced = partialIfReduced,
            DoneStatus = doneStatus,
            DoneAccount = doneAccount
        };
    }

    private async Task<string> RenderSageAsync(ForwardingSageModel sage, MembershipApplicationDTO? application = null) {
        application ??= Application();
        var (forwardingCase, _) = Case(ForwardingPurpose.Application, _federal.Id);
        forwardingCase.PayloadJson = null;
        var step = new ForwardingStep { CaseId = forwardingCase.Id, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Application, Order = 2 };
        return await _renderer.RenderSageRequestAsync(forwardingCase, step, application, sage);
    }

    [Test]
    public async Task Sage_request_lists_number_entry_and_partial_due() {
        var text = await RenderSageAsync(Sage());

        await Assert.That(text).Contains("Vorläufige Mitgliedsnummer: RM1001");
        await Assert.That(text).Contains("Name: Anna Pirat");
        await Assert.That(text).Contains("Gewünschtes Eintrittsdatum: 01.11.2026");
        await Assert.That(text).Contains("Beitrag 2026 anteilig ab November: 20 €");
        await Assert.That(text).Contains("Jahresbeitrag: 120 €");
        await Assert.That(text).Contains("Lastschrift: Anna Pirat, IBAN DE02120300000000202051");
    }

    [Test]
    public async Task Sage_request_renders_the_full_default_body() {
        var text = await RenderSageAsync(Sage(doneStatus: "In SAGE erfasst"));

        await Assert.That(text).IsEqualTo("**Bitte um Eintragung in SAGE**\n\n- Vorläufige Mitgliedsnummer: RM1001\n- Name: Anna Pirat\n- Gewünschtes Eintrittsdatum: 01.11.2026\n"
            + "- Beitrag 2026 anteilig ab November: 20 €\n\n**Beitrag**\n\n- Einstufung: nach monatlichem Einkommen\n- Jahresbeitrag: 120 €\n- Zahlweise: jährlich\n"
            + "- Lastschrift: Anna Pirat, IBAN DE02120300000000202051\n\nDie übrigen Personendaten stehen in der Ticketbeschreibung. "
            + "Sobald das Ticket auf „In SAGE erfasst“ steht, verschickt Quartermaster die Aufnahmebestätigung.");
    }

    [Test]
    public async Task Sage_request_without_due_selection_has_no_due_lines() {
        var application = Application();
        application.DueSelection = null;
        var sage = Sage(doneStatus: "In SAGE erfasst");
        sage.YearlyAmount = "";
        sage.PartialAmount = "";

        var text = await RenderSageAsync(sage, application);

        await Assert.That(text).Contains("- Gewünschtes Eintrittsdatum: 01.11.2026\n\nDie übrigen Personendaten stehen in der Ticketbeschreibung.");
        await Assert.That(text).DoesNotContain("anteilig");
        await Assert.That(text).DoesNotContain("**Beitrag**");
        await Assert.That(text).DoesNotContain("Jahresbeitrag");
        await Assert.That(text).DoesNotContain("€");
    }

    [Test]
    public async Task Sage_request_with_open_reduction_names_both_amounts() {
        var text = await RenderSageAsync(Sage("offen", "6"));

        await Assert.That(text).Contains("6 € bei Genehmigung der Minderung, sonst 20 €");
    }

    [Test]
    public async Task Sage_request_done_hint_status_only() {
        var text = await RenderSageAsync(Sage(doneStatus: "In SAGE erfasst"));

        await Assert.That(text).Contains("Sobald das Ticket auf „In SAGE erfasst“ steht, verschickt Quartermaster die Aufnahmebestätigung.");
    }

    [Test]
    public async Task Sage_request_done_hint_account_only() {
        var text = await RenderSageAsync(Sage(doneAccount: "qm-bot"));

        await Assert.That(text).Contains("Sobald das Ticket wieder dem Konto qm-bot zugewiesen ist, verschickt Quartermaster die Aufnahmebestätigung.");
    }

    [Test]
    public async Task Sage_request_done_hint_both() {
        var text = await RenderSageAsync(Sage(doneStatus: "In SAGE erfasst", doneAccount: "qm-bot"));

        await Assert.That(text).Contains("Sobald das Ticket auf „In SAGE erfasst“ steht und das Ticket wieder dem Konto qm-bot zugewiesen ist, verschickt Quartermaster die Aufnahmebestätigung.");
    }

    [Test]
    [Arguments(ForwardingStepKind.CreateTicket, "nicht angelegt werden")]
    [Arguments(ForwardingStepKind.AddOutcomeNote, "nicht um die Entscheidung ergänzt werden")]
    [Arguments(ForwardingStepKind.AwaitSageEntry, "nicht auf die SAGE-Eintragung geprüft werden")]
    public async Task Failure_mail_names_the_action(ForwardingStepKind kind, string expected) {
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, Application());
        step.Kind = kind;
        step.LastError = "Fehler";

        var (_, html) = await _renderer.RenderFailureMailAsync(forwardingCase, step, null);

        await Assert.That(html).Contains(expected);
    }

    [Test]
    public async Task Failure_mail_of_a_ticket_asks_to_create_it_by_hand() {
        var (forwardingCase, step) = Case(ForwardingPurpose.Application, _federal.Id, Application());
        step.LastError = "Fehler";

        var (_, html) = await _renderer.RenderFailureMailAsync(forwardingCase, step, null);

        await Assert.That(html).Contains("Bitte das Ticket von Hand anlegen und den Vorgang danach unter");
        await Assert.That(html).Contains("qm.test/Administration/Forwarding");
        await Assert.That(html).DoesNotContain("Aufnahmebestätigung");
    }

    [Test]
    [Arguments(ForwardingStepKind.AddOutcomeNote, true)]
    [Arguments(ForwardingStepKind.AwaitSageEntry, true)]
    [Arguments(ForwardingStepKind.AddOutcomeNote, false)]
    public async Task Failure_mail_of_a_later_step_asks_to_fix_and_retry(ForwardingStepKind kind, bool applicationCase) {
        var (forwardingCase, step) = applicationCase
            ? Case(ForwardingPurpose.Application, _federal.Id, Application())
            : Case(ForwardingPurpose.Reduction, _federal.Id, due: new DueSelectionDTO { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 24 });
        step.Kind = kind;
        step.LastError = "Fehler";

        var (_, html) = await _renderer.RenderFailureMailAsync(forwardingCase, step, null);

        await Assert.That(html).Contains("Bitte die Ursache beheben und den Vorgang unter");
        await Assert.That(html).Contains("qm.test/Administration/Forwarding");
        await Assert.That(html).DoesNotContain("Ticket von Hand anlegen");
        if (applicationCase) {
            await Assert.That(html).Contains("Wird der Vorgang von Hand als erledigt markiert, verschickt Quartermaster keine Aufnahmebestätigung.");
        } else {
            await Assert.That(html).DoesNotContain("Aufnahmebestätigung");
        }
    }
}
