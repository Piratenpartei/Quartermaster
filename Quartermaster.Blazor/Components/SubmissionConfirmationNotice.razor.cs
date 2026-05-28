using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class SubmissionConfirmationNotice {
    [Parameter]
    public string Email { get; set; } = "";
}
