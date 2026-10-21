using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

/// <summary>For visitors who are not signed in: why to sign in, where to join the party, and the SSO login one click away.</summary>
public partial class SignInPrompt {
    private const string ExternalApplicationUrl = "https://members.piratenpartei.de/";

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ClientConfigService Config { get; set; }

    private string JoinUrl() {
        return Config.MembershipApplicationEnabled ? "/MembershipApplication" : ExternalApplicationUrl;
    }

    private string? JoinTarget() {
        return Config.MembershipApplicationEnabled ? null : "_blank";
    }
}
