using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionRankingTable {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyMotionRankingItemDTO> Items { get; set; }
}
