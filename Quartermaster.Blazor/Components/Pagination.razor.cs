using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class Pagination {
    [Parameter]
    public int CurrentPage { get; set; } = 1;

    [Parameter]
    public int TotalPages { get; set; }

    [Parameter]
    public EventCallback<int> OnPageChanged { get; set; }

    private async Task SetPage(int selectedPage) {
        if (selectedPage < 1 || selectedPage > TotalPages || selectedPage == CurrentPage)
            return;

        await OnPageChanged.InvokeAsync(selectedPage);
    }

    private async Task OnJumpToPage(ChangeEventArgs e) {
        if (int.TryParse(e.Value?.ToString(), out var selectedPage))
            await SetPage(selectedPage);
    }

    private IEnumerable<int> GetPageNumbers() {
        var start = Math.Max(1, CurrentPage - 2);
        var end = Math.Min(TotalPages, CurrentPage + 2);

        for (var i = start; i <= end; i++)
            yield return i;
    }
}
