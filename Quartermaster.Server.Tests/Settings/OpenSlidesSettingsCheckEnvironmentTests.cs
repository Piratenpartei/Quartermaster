using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Options;
using Quartermaster.Server.Settings.Checks;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Settings;

[NotInParallel("FakeOpenSlidesClient")]
public class OpenSlidesSettingsCheckEnvironmentTests : RepositoryTestBase {
    [Test]
    public async Task Plain_http_to_localhost_is_refused_outside_development() {
        FakeOpenSlidesClient.Reset();
        var options = new OptionRepository(Db, AuditLog);
        options.SupplementDefaults();
        options.SetValue("openslides.url", null, "http://localhost:8000");
        options.SetValue("openslides.username", null, "assistant");
        options.SetValue("openslides.password", null, "os-secret");
        var check = new OpenSlidesSettingsCheck(new FakeOpenSlidesClient(), options, new HostingEnvironment { EnvironmentName = "Production" },
            NullLogger<OpenSlidesSettingsCheck>.Instance);

        var result = await check.RunAsync(CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.SettingsCheck.InsecureUrl);
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(0);
    }
}
