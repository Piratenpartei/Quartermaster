namespace Quartermaster.Api.Config;

public class ClientConfigDTO {
    public string ErrorContact { get; set; } = "";
    public bool ShowDetailedErrors { get; set; }
    public bool SamlEnabled { get; set; }
    public string SamlButtonText { get; set; } = "";
    public string SsoSupportContact { get; set; } = "";
}
