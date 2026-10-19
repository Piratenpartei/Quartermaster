using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

[NotInParallel("FakeDiscourseClient")]
public class AssemblyMotionForumConnectionCheckTests : RepositoryTestBase {
    private OptionRepository _options = default!;
    private AssemblyMotionForumConnectionCheck _check = default!;

    [Before(Test)]
    public void Setup() {
        FakeDiscourseClient.Reset();
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
        _options.SetValue("discourse.base_url", null, "https://forum.test");
        _options.SetValue("discourse.api_username", null, "antragsbot");
        _options.SetValue("discourse.api_key", null, "key");
        FakeDiscourseClient.Categories[7] = new DiscourseCategory(7, "Anträge", true, true);
        _check = new AssemblyMotionForumConnectionCheck(new FakeDiscourseClient(), _options, NullLogger<AssemblyMotionForumConnectionCheck>.Instance);
    }

    [Test]
    public async Task Unconfigured_is_reported() {
        _options.SetValue("discourse.api_key", null, "");

        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Configured).IsFalse();
        await Assert.That(result.Ok).IsFalse();
        await Assert.That(FakeDiscourseClient.Calls).IsEmpty();
    }

    [Test]
    public async Task Success_names_account_and_category() {
        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Message).Contains("antragsbot");
        await Assert.That(result.Message).Contains("Anträge");
        await Assert.That(result.Warnings).IsEmpty();
    }

    [Test]
    public async Task Unauthorized_fails_with_fixed_text() {
        FakeDiscourseClient.ThrowOn["CurrentUser"] = new DiscourseException("SECRET-BODY", DiscourseException.Unauthorized, 403);

        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Das Forum hat den API-Schlüssel oder das Bot-Konto abgelehnt.");
    }

    [Test]
    public async Task Unexpected_failure_never_leaks_the_exception_text() {
        FakeDiscourseClient.ThrowOn["CurrentUser"] = new InvalidOperationException("SECRET-DETAIL");

        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).DoesNotContain("SECRET");
    }

    [Test]
    public async Task Non_moderator_is_a_warning() {
        FakeDiscourseClient.CurrentUser = new DiscourseCurrentUser("antragsbot", false);

        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Warnings.Single()).IsEqualTo("Das Konto antragsbot ist kein Moderator; Discourse begrenzt dann Themen, Bearbeitungen und Links.");
    }

    [Test]
    public async Task Missing_category_fails() {
        var result = await _check.CheckAsync(8, CancellationToken.None);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Die Forum-Kategorie 8 existiert nicht.");
    }

    [Test]
    public async Task Category_without_create_permission_fails() {
        FakeDiscourseClient.Categories[7] = new DiscourseCategory(7, "Anträge", true, false);

        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Das Konto darf in dieser Kategorie keine Themen anlegen.");
    }

    [Test]
    public async Task Public_category_is_a_warning() {
        FakeDiscourseClient.Categories[7] = new DiscourseCategory(7, "Anträge", false, true);

        var result = await _check.CheckAsync(7, CancellationToken.None);

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Warnings.Single()).IsEqualTo("Die Kategorie ist öffentlich lesbar; die Beiträge enthalten die Namen der Antragsteller.");
    }

    [Test]
    public async Task Without_category_only_the_account_is_checked() {
        var result = await _check.CheckAsync(null, CancellationToken.None);

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(FakeDiscourseClient.Count("Category")).IsEqualTo(0);
    }
}
