using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Settings.Checks;

public class KeycloakSettingsCheck : ISettingsCheck {
    private readonly KeycloakSettings _settings;
    private readonly IKeycloakAdminClient _client;
    private readonly ILogger<KeycloakSettingsCheck> _logger;

    public KeycloakSettingsCheck(KeycloakSettings settings, IKeycloakAdminClient client, ILogger<KeycloakSettingsCheck> logger) {
        _settings = settings;
        _client = client;
        _logger = logger;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.Keycloak;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        if (!_settings.IsConfigured)
            return SettingsCheckResults.NotConfigured();

        try {
            await _client.CheckAccessAsync(ct);
            return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.KeycloakOk, ("realm", _settings.Realm)));
        } catch (KeycloakException ex) {
            _logger.LogWarning("Keycloak settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, ex.Message);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Keycloak settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, SettingsCheckResults.UnexpectedErrorDetail);
        }
    }
}
