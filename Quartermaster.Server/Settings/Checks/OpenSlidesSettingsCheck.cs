using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.Settings.Checks;

public class OpenSlidesSettingsCheck : ISettingsCheck {
    private readonly IOpenSlidesClient _client;
    private readonly OptionRepository _options;
    private readonly ILogger<OpenSlidesSettingsCheck> _logger;
    private readonly bool _allowLoopbackHttp;

    public OpenSlidesSettingsCheck(IOpenSlidesClient client, OptionRepository options, IHostEnvironment environment, ILogger<OpenSlidesSettingsCheck> logger) {
        _client = client;
        _allowLoopbackHttp = environment.IsDevelopment();
        _options = options;
        _logger = logger;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.OpenSlides;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        var settings = OpenSlidesSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return SettingsCheckResults.NotConfigured();
        if (!OpenSlidesSettings.IsSecureUrl(settings.Url, _allowLoopbackHttp))
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.InsecureUrl);

        try {
            await _client.LoginAsync(settings, ct);
            return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.LoginOk, ("username", settings.Username)));
        } catch (OpenSlidesException ex) {
            _logger.LogWarning("OpenSlides settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, ex.Message);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("OpenSlides settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, SettingsCheckResults.UnexpectedErrorDetail);
        }
    }
}
