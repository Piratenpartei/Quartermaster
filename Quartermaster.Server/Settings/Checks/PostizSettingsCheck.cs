using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.Settings.Checks;

public class PostizSettingsCheck : ISettingsCheck {
    private readonly NewsPostsSettings _settings;
    private readonly IPostizClient _client;
    private readonly ILogger<PostizSettingsCheck> _logger;

    public PostizSettingsCheck(NewsPostsSettings settings, IPostizClient client, ILogger<PostizSettingsCheck> logger) {
        _settings = settings;
        _client = client;
        _logger = logger;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.Postiz;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        if (!_settings.HasPostiz)
            return SettingsCheckResults.NotConfigured();

        try {
            var channels = await _client.GetChannelsAsync(ct);
            var names = channels
                .Where(c => !c.Disabled)
                .Select(c => $"{c.Name} ({c.Identifier})")
                .ToList();
            return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.PostizOk, ("count", names.Count.ToString())), names);
        } catch (PostizException ex) {
            _logger.LogWarning("Postiz settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, ex.Message);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Postiz settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, SettingsCheckResults.UnexpectedErrorDetail);
        }
    }
}
