using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.ApplicantRegister;

public partial class ApplicantRegisterList {
    private const int PageSize = 25;

    [Inject]
    public required ApplicantRegisterService Register { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private ApplicantRegisterEntrySearchResponse? Response;
    private string? SearchQuery;
    private int CurrentPage = 1;

    private int TotalPages => Response == null ? 0 : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        if (!AuthService.HasGlobalPermission(PermissionIdentifier.ManageApplicantRegister))
            return;

        await Search();
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter")
            await SearchFromStart();
    }

    private async Task SearchFromStart() {
        CurrentPage = 1;
        await Search();
    }

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await Search();
    }

    private async Task Search() {
        Response = await Register.SearchAsync(SearchQuery, CurrentPage, PageSize) ?? Response ?? new ApplicantRegisterEntrySearchResponse();
    }

    private string KindLabel(ApplicantRegisterKind kind) {
        return ApplicantRegisterKindLabel.Of(I18n, kind);
    }
}
