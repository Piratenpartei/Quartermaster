using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionFieldsForm {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required AssemblyMotionFieldValues Values { get; set; }

    [Parameter, EditorRequired]
    public required AssemblyMotionPortalDTO Portal { get; set; }

    [Parameter]
    public bool TypeLocked { get; set; }

    [Parameter]
    public EventCallback TypeChanged { get; set; }

    private AssemblyMotionTypeDTO? SelectedType => Portal.Types.FirstOrDefault(t => t.Id.ToString() == Values.TypeId);

    private async Task OnTypeChanged(string value) {
        Values.ChangeType(value, Portal.Types);
        await TypeChanged.InvokeAsync();
    }
}
