using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.SupportSignatures;

public enum CampaignTab {
    Overview,
    Intake,
    Shipments,
    Settings
}

public partial class SupportSignatureCampaign {
    private static readonly CampaignTab[] Tabs = [CampaignTab.Overview, CampaignTab.Intake, CampaignTab.Shipments, CampaignTab.Settings];

    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private SignatureCampaignDTO? Campaign;
    private bool Loading = true;
    private CampaignTab ActiveTab = CampaignTab.Overview;

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        try {
            Campaign = await Signatures.CampaignAsync(Id);
        } finally {
            Loading = false;
        }
    }

    private async Task ReloadAsync() {
        Campaign = await Signatures.CampaignAsync(Id) ?? Campaign;
    }

    private void OnSettingsChanged(SignatureCampaignDTO updated) {
        Campaign = updated;
    }

    private string TabLabel(CampaignTab tab) {
        return tab switch {
            CampaignTab.Overview => I18n[I18nKey.Ui.SupportSignatures.TabOverview],
            CampaignTab.Intake => I18n[I18nKey.Ui.SupportSignatures.TabIntake],
            CampaignTab.Shipments => I18n[I18nKey.Ui.SupportSignatures.TabShipments],
            _ => I18n[I18nKey.Ui.SupportSignatures.TabSettings]
        };
    }
}
