using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Templates;

/// <summary>Seed changes reach the system templates nobody edited, so text fixes need no manual edits in production.</summary>
public class SystemTemplateSeedUpdateTests : RepositoryTestBase {
    private const string Identifier = "notifications.assembly_motion_withdrawn.email";
    private const string EarlierSeedBody = "Hallo,\n\nein früherer Text.\n";
    private const string EditedBody = "Hallo,\n\nvon Hand geändert.\n";

    private TemplateRepository _templates = default!;

    [Before(Test)]
    public void Setup() {
        _templates = new TemplateRepository(Db);
        _templates.SupplementDefaults();
    }

    private Template Stored(string identifier = Identifier) {
        return _templates.GetSystemBase(identifier)!;
    }

    private void SetStored(Template template, string displayName, string body, string? seedHash) {
        Db.Templates
            .Where(t => t.Id == template.Id)
            .Set(t => t.DisplayName, displayName)
            .Set(t => t.Body, body)
            .Set(t => t.SeedHash, seedHash)
            .Update();
    }

    [Test]
    public async Task A_new_system_template_records_the_hash_of_its_seed() {
        var stored = Stored();

        await Assert.That(stored.SeedHash).IsEqualTo(TemplateSeedHash.Compute(stored.DisplayName, stored.Subject, stored.Body));
    }

    [Test]
    public async Task An_unedited_template_follows_a_changed_seed() {
        var seeded = Stored();
        SetStored(seeded, seeded.DisplayName, EarlierSeedBody, TemplateSeedHash.Compute(seeded.DisplayName, seeded.Subject, EarlierSeedBody));

        _templates.SupplementDefaults();

        var stored = Stored();
        await Assert.That(stored.Body).IsEqualTo(seeded.Body);
        await Assert.That(stored.SeedHash).IsEqualTo(seeded.SeedHash);
    }

    [Test]
    public async Task An_edited_template_is_kept_when_its_seed_changes() {
        var seeded = Stored();
        SetStored(seeded, seeded.DisplayName, EditedBody, TemplateSeedHash.Compute(seeded.DisplayName, seeded.Subject, EarlierSeedBody));

        _templates.SupplementDefaults();

        await Assert.That(Stored().Body).IsEqualTo(EditedBody);
    }

    [Test]
    public async Task A_template_without_hash_that_matches_its_seed_gets_the_hash() {
        var seeded = Stored();
        SetStored(seeded, seeded.DisplayName, seeded.Body, null);

        _templates.SupplementDefaults();

        await Assert.That(Stored().SeedHash).IsEqualTo(seeded.SeedHash);
    }

    [Test]
    public async Task A_template_without_hash_and_another_text_is_kept() {
        var seeded = Stored();
        SetStored(seeded, seeded.DisplayName, EditedBody, null);

        _templates.SupplementDefaults();

        var stored = Stored();
        await Assert.That(stored.Body).IsEqualTo(EditedBody);
        await Assert.That(stored.SeedHash).IsNull();
    }

    /// <summary>Production rows seeded by V9.0 carry no hash; the V9.0 text itself marks them as unedited.</summary>
    [Test]
    public async Task A_template_still_holding_its_V9_0_text_gets_the_new_seed() {
        const string joined = "notifications.assembly_motion_cosubmitter_joined.telegram";
        var seeded = Stored(joined);
        SetStored(seeded,
            "Benachrichtigung: Neue:r Mitantragsteller:in (Telegram)",
            "Neue Mitantragstellung für *{{ motion.TitleEscaped }}*: jetzt {{ motion.SubmitterCount }} von {{ motion.MinSubmitters }} nötigen Antragstellenden.{% if motion.SubmitterCount >= motion.MinSubmitters %} Damit ist der Antrag eingereicht.{% endif %}\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            null);

        _templates.SupplementDefaults();

        var stored = Stored(joined);
        await Assert.That(stored.DisplayName).IsEqualTo("Benachrichtigung: Neuer Mitantragsteller (Telegram)");
        await Assert.That(stored.Body).Contains("von {{ motion.MinSubmitters }} nötigen Antragstellern.");
        await Assert.That(stored.SeedHash).IsEqualTo(TemplateSeedHash.Compute(stored));
    }

    [Test]
    public async Task The_seed_hash_covers_name_subject_and_body() {
        var hash = TemplateSeedHash.Compute("Name", "Betreff", "Text");

        await Assert.That(TemplateSeedHash.Compute("Name 2", "Betreff", "Text")).IsNotEqualTo(hash);
        await Assert.That(TemplateSeedHash.Compute("Name", "Betreff 2", "Text")).IsNotEqualTo(hash);
        await Assert.That(TemplateSeedHash.Compute("Name", null, "Text")).IsNotEqualTo(TemplateSeedHash.Compute("Name", "", "Text"));
        await Assert.That(TemplateSeedHash.Compute("Name", "Betreff", "Text 2")).IsNotEqualTo(hash);
    }
}
