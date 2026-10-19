using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Forum;

namespace Quartermaster.Server.Settings.Checks;

public class ForumSettingsCheck : ISettingsCheck {
    private readonly AssemblyMotionForumConnectionCheck _check;
    private readonly OptionRepository _options;

    public ForumSettingsCheck(AssemblyMotionForumConnectionCheck check, OptionRepository options) {
        _check = check;
        _options = options;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.Forum;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        var result = await _check.CheckAsync(null, ct);
        if (!result.Configured)
            return SettingsCheckResults.NotConfigured();
        if (!result.Ok)
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, result.Message);

        var username = DiscourseSettings.ReadFrom(_options).ApiUsername;
        return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.LoginOk, ("username", username)), result.Warnings.ToList());
    }
}
