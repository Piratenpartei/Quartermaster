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

    public async Task LoadAsync() {
        try {
            _config = await _http.GetFromJsonAsync<ClientConfigDTO>("/api/config/client");
        } catch {
            _config = new ClientConfigDTO();
        }
    }
}
