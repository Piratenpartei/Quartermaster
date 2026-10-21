using System.Globalization;
using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

/// <summary>
/// Certified signatures against the goal (target plus margin), with a mark where the bare target lies. A lighter part continues the bar up to
/// everything collected that was not rejected, so the forms still waiting or at the offices show as well.
/// </summary>
public partial class SupportSignatureProgress {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public int Certified { get; set; }

    [Parameter]
    public int Collected { get; set; }

    [Parameter]
    public int Target { get; set; }

    [Parameter]
    public int Goal { get; set; }

    private string Percent(int value) {
        return Fraction(value).ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>The width of the part after the certified one, so both together never pass the end of the bar.</summary>
    private string Share(int value) {
        var share = Math.Max(0, Fraction(Certified + value) - Fraction(Certified));
        return share.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private double Fraction(int value) {
        return Goal <= 0 ? 0 : Math.Clamp(value * 100.0 / Goal, 0, 100);
    }
}
