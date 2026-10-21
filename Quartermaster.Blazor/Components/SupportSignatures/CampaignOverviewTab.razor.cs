using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public partial class CampaignOverviewTab {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid CampaignId { get; set; }

    private SignatureOverviewDTO? Overview;
    private readonly HashSet<Guid> Expanded = [];

    protected override async Task OnParametersSetAsync() {
        Overview = await Signatures.OverviewAsync(CampaignId);
    }

    private void Toggle(Guid regionId) {
        if (!Expanded.Remove(regionId))
            Expanded.Add(regionId);
    }

    private List<(string Label, int Value, string Css)> Figures() {
        var totals = Overview!.Totals;
        return [
            (I18n[I18nKey.Ui.SupportSignatures.Received], totals.Received, ""),
            (I18n[I18nKey.Ui.SupportSignatures.Waiting], totals.Waiting, ""),
            (I18n[I18nKey.Ui.SupportSignatures.AtOffices], totals.AtOffices, ""),
            (I18n[I18nKey.Ui.SupportSignatures.Certified], totals.Certified, "text-success"),
            (I18n[I18nKey.Ui.SupportSignatures.Rejected], totals.Rejected, totals.Rejected > 0 ? "text-danger" : "")
        ];
    }
}
