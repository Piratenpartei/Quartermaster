using Quartermaster.Data.Migrations;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class SystemTemplateSeedTests : RepositoryTestBase {
    private TemplateRepository _templates = default!;

    [Before(Test)]
    public void Setup() {
        _templates = new TemplateRepository(Db);
        _templates.SupplementDefaults();
    }

    private const string LegacyFailureBody = "Für den Vorgang **{{ case.Reference }}** ({{ case.Kind }}) konnte das Ticket \"{{ failure.Purpose }}\" bei **{{ failure.TargetChapter }}** nicht angelegt werden.\n\n**Fehler:** {{ failure.Error }}\n\nBitte das Ticket von Hand anlegen und den Vorgang danach unter {{ failure.CasesUrl }} als erledigt markieren, oder die Ursache beheben und dort erneut versuchen.\n\n---\n\n**Betreff:** {{ failure.TicketSubject }}\n\n{{ failure.TicketDescription }}\n";

    [Test]
    public async Task Migrated_failure_mail_body_equals_the_seed_and_the_migration_is_idempotent() {
        var seed = _templates.GetSystemBase(M009.FailureMailIdentifier)!.Body;

        var migrated = MigrateFailureBody(LegacyFailureBody);

        await Assert.That(migrated).IsEqualTo(seed);
        await Assert.That(MigrateFailureBody(migrated)).IsEqualTo(migrated);
    }

    [Test]
    public async Task Rolled_back_failure_mail_body_equals_the_legacy_body() {
        var seed = _templates.GetSystemBase(M009.FailureMailIdentifier)!.Body;

        var rolledBack = seed.Replace(M009.FailureRemedyTag, M009.FailureLegacyRemedy).Replace(M009.FailureActionPhrase, M009.FailureLegacyPhrase);

        await Assert.That(rolledBack).IsEqualTo(LegacyFailureBody);
    }

    private static string MigrateFailureBody(string body) {
        return body.Replace(M009.FailureLegacyPhrase, M009.FailureActionPhrase).Replace(M009.FailureLegacyRemedy, M009.FailureRemedyTag);
    }

    [Test]
    public async Task Sage_request_seed_has_no_subject_and_no_chapter_fields() {
        var template = _templates.GetSystemBase(ForwardingTicketRenderer.SageRequestTemplate)!;

        await Assert.That(template.Subject).IsNull();
        await Assert.That(template.AllowsChapterFields).IsFalse();
    }
}
