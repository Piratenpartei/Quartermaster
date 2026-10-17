using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Config;

namespace Quartermaster.Blazor.Services;

public class ClientConfigService {
    private readonly HttpClient _http;
    private ClientConfigDTO? _config;

    public ClientConfigService(HttpClient http) {
        _http = http;
    }

    public string ErrorContact => _config?.ErrorContact ?? "";
    public bool ShowDetailedErrors => _config?.ShowDetailedErrors ?? false;
    public bool SamlEnabled => _config?.SamlEnabled ?? false;
    public string SamlButtonText => _config?.SamlButtonText ?? "SSO Login";
    public string SsoSupportContact => _config?.SsoSupportContact ?? "";
    public bool OidcEnabled => _config?.OidcEnabled ?? false;
    public string OidcButtonText => _config?.OidcButtonText ?? "OpenID Login";
    public bool IsDebug => _config?.IsDebug ?? false;
    public bool MembershipApplicationEnabled => _config?.MembershipApplicationEnabled ?? false;
    public bool DueSelectionEnabled => _config?.DueSelectionEnabled ?? false;
    public IReadOnlyList<Guid> MotionFormChapterIds => _config?.MotionFormChapterIds ?? [];
    public bool MotionFormEnabled => MotionFormChapterIds.Count > 0;

    public async Task LoadAsync(bool forceRefresh = false) {
        if (_config != null && !forceRefresh)
            return;

        try {
            _config = await _http.GetFromJsonAsync<ClientConfigDTO>("/api/config/client");
        } catch (Exception ex) {
            System.Console.Error.WriteLine($"ClientConfigService.LoadAsync: config fetch failed, using defaults. {ex}");
            _config ??= new ClientConfigDTO();
        }
    }
}
