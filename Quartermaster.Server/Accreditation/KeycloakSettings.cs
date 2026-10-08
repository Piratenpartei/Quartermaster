using Quartermaster.Data.Options;

namespace Quartermaster.Server.Accreditation;

public class KeycloakSettings {
    public string BaseUrl { get; init; } = "";
    public string AuthRealm { get; init; } = "master";
    public string Realm { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string UsernameSeparator { get; init; } = ".";

    public bool IsConfigured => BaseUrl.Length > 0 && Realm.Length > 0 && ClientId.Length > 0 && ClientSecret.Length > 0;

    public static KeycloakSettings ReadFrom(OptionRepository options) {
        string Value(string id, string fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        return new KeycloakSettings {
            BaseUrl = Value("keycloak.base_url", "").TrimEnd('/'),
            AuthRealm = Value("keycloak.auth_realm", "master"),
            Realm = Value("keycloak.realm", ""),
            ClientId = Value("keycloak.client_id", ""),
            ClientSecret = Value("keycloak.client_secret", ""),
            UsernameSeparator = Value("keycloak.username_separator", ".")
        };
    }
}
