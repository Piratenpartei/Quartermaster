using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.Settings.Checks;

/// <summary>Lists the models the key can use; the assistant offers them as suggestions for the model fields.</summary>
public class MistralSettingsCheck : ISettingsCheck {
    private readonly NewsPostsSettings _settings;
    private readonly IMistralClient _client;
    private readonly ILogger<MistralSettingsCheck> _logger;

    public MistralSettingsCheck(NewsPostsSettings settings, IMistralClient client, ILogger<MistralSettingsCheck> logger) {
        _settings = settings;
        _client = client;
        _logger = logger;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.Mistral;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        if (!_settings.HasMistral)
            return SettingsCheckResults.NotConfigured();

        try {
            var models = await _client.ListModelsAsync(ct);
            return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.MistralOk, ("count", models.Count.ToString())), models);
        } catch (MistralException ex) {
            _logger.LogWarning("Mistral settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, ex.Message);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Mistral settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, SettingsCheckResults.UnexpectedErrorDetail);
        }
    }
}
