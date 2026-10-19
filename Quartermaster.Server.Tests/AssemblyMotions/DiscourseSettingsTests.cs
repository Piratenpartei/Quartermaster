using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class DiscourseSettingsTests : RepositoryTestBase {
    private OptionRepository _options = default!;

    [Before(Test)]
    public void Setup() {
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
    }

    [Test]
    public async Task ReadFrom_trims_and_strips_trailing_slash() {
        _options.SetValue("discourse.base_url", null, "  https://forum.example.org/  ");
        _options.SetValue("discourse.api_username", null, " antragsbot ");
        _options.SetValue("discourse.api_key", null, " key ");

        var settings = DiscourseSettings.ReadFrom(_options);

        await Assert.That(settings.BaseUrl).IsEqualTo("https://forum.example.org");
        await Assert.That(settings.ApiUsername).IsEqualTo("antragsbot");
        await Assert.That(settings.ApiKey).IsEqualTo("key");
        await Assert.That(settings.IsConfigured()).IsTrue();
    }

    [Test]
    [Arguments("", 32000)]
    [Arguments("abc", 32000)]
    [Arguments("500", 32000)]
    [Arguments("50000", 50000)]
    public async Task MaxPostLength_falls_back_to_default(string value, int expected) {
        _options.SetValue("discourse.max_post_length", null, value);

        await Assert.That(DiscourseSettings.ReadFrom(_options).MaxPostLength).IsEqualTo(expected);
    }

    [Test]
    public async Task ToString_never_contains_the_key() {
        var settings = new DiscourseSettings("https://forum.example.org", "bot", "very-secret-key", 32000);

        await Assert.That(settings.ToString()).DoesNotContain("very-secret-key");
    }

    [Test]
    public async Task TopicUrl_uses_base_url() {
        var settings = new DiscourseSettings("https://forum.example.org", "bot", "key", 32000);

        await Assert.That(settings.TopicUrl(42)).IsEqualTo("https://forum.example.org/t/42");
    }

    [Test]
    public async Task Missing_key_is_not_configured() {
        _options.SetValue("discourse.base_url", null, "https://forum.example.org");
        _options.SetValue("discourse.api_username", null, "bot");

        await Assert.That(DiscourseSettings.ReadFrom(_options).IsConfigured()).IsFalse();
    }
}
