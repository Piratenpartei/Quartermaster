namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

public record RecordedWikiRequest(
    HttpMethod Method,
    string Url,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyDictionary<string, string> Headers,
    string RawBody) {
    public string Action => Fields.GetValueOrDefault("action", "");
}
