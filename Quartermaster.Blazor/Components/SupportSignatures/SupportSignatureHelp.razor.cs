using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.SupportSignatures;

/// <summary>What support signatures are and what to do with the form, for people who have never collected any.</summary>
public partial class SupportSignatureHelp {
    [Inject]
    public required I18nService I18n { get; set; }
}
