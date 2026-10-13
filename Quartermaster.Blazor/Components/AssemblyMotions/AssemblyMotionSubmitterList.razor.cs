using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionSubmitterList {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyMotionSubmitterDTO> Submitters { get; set; }

    private string MemberNumberLabel(int memberNumber) {
        return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.MemberNumber, ("number", memberNumber.ToString()))];
    }
}
