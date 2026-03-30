using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Members;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MemberDetailDTO? Member;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Member = await Http.GetFromJsonAsync<MemberDetailDTO>($"/api/members/{Id}");
        } catch (HttpRequestException) { }

        Loading = false;
    }
}
