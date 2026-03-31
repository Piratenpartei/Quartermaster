using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Blazor.Pages.Administration;

public class ChapterDetailResponse {
    public ChapterDTO Chapter { get; set; } = new();
    public Guid? ParentChapterId { get; set; }
    public string? ParentChapterName { get; set; }
    public List<ChapterOfficerDTO> Officers { get; set; } = new();
    public List<ChapterDTO> Children { get; set; } = new();
}

public partial class ChapterDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ChapterDetailResponse? Detail;
    private bool Loading = true;

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        Detail = null;

        try {
            Detail = await Http.GetFromJsonAsync<ChapterDetailResponse>($"/api/chapters/{Id}");
        } catch (HttpRequestException) { }

        Loading = false;
    }

    private static string RoleLabel(int role) => role switch {
        0 => "Vorsitzender",
        1 => "Stellv. Vorsitzender",
        2 => "Quartiermeister",
        3 => "Schatzmeister",
        4 => "Stellv. Schatzmeister",
        5 => "Pol. Geschäftsführer",
        6 => "Beisitzer",
        _ => "Unbekannt"
    };
}
