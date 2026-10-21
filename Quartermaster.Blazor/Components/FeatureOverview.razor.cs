using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

/// <summary>What Quartermaster offers, for visitors and for chapters that might want to use it.</summary>
public partial class FeatureOverview {
    private const string SupportEmail = "support@it.piratenpartei.de";

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ClientConfigService Config { get; set; }

    /// <summary>Only the public switches count here, not the officers' access for entering paper forms.</summary>
    private bool ShowMembershipForms() {
        return Config.MembershipApplicationEnabled && Config.DueSelectionEnabled;
    }
}
