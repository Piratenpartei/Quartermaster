using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class LogPanel {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyNoticeDTO> Notices { get; set; }
}
