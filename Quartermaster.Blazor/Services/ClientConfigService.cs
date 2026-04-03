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

    public async Task LoadAsync(bool forceRefresh = false) {
        if (_config != null && !forceRefresh)
            return;

        try {
            _config = await _http.GetFromJsonAsync<ClientConfigDTO>("/api/config/client");
        } catch {
            _config ??= new ClientConfigDTO();
        }
    }
}
