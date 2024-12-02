namespace Quartermaster.Data;

public class SamlSettings {
    public required string SamlEndpoint { get; set; }
    public required string SamlClientId { get; set; }
    public required string SamlCertificate { get; set; }
}