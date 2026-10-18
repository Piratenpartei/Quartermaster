using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.Settings.Checks;

public class WikiSettingsCheck : ISettingsCheck {
    private readonly AssemblyMotionWikiConnectionCheck _check;
    private readonly OptionRepository _options;

    public WikiSettingsCheck(AssemblyMotionWikiConnectionCheck check, OptionRepository options) {
        _check = check;
        _options = options;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.Wiki;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        var result = await _check.CheckAsync(ct);
        if (!result.Configured)
            return SettingsCheckResults.NotConfigured();
        if (!result.Ok)
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, result.Message);

        var username = WikiBotSettings.ReadFrom(_options).Username;
        return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.LoginOk, ("username", username)));
    }
}
