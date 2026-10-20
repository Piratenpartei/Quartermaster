using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.Forwarding.Redmine;

public record RedmineConnectionSettings(string BaseUrl, string ApiKey) {
    public string Root => BaseUrl.Trim().TrimEnd('/');

    /// <inheritdoc cref="WikiBotSettings.IsSecureUrl"/>
    public bool IsSecureUrl(bool allowLoopbackHttp) {
        return WikiBotSettings.IsSecureUrl(Root, allowLoopbackHttp);
    }

    public string IssueUrl(int issueId) {
        return $"{Root}/issues/{issueId}";
    }

    // The generated record ToString would print the API key.
    public override string ToString() {
        return $"RedmineConnectionSettings {{ BaseUrl = {BaseUrl} }}";
    }
}
