using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public partial class SupportSignatureProgressCard {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MemberSignatureCampaignDTO Campaign { get; set; } = default!;

    private async Task DownloadForm() {
        await Signatures.DownloadMemberFormAsync(Campaign.Id, Campaign.Name);
    }
}
