using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.ApplicantRegister;

public partial class ApplicantRegisterEdit {
    private const string ListPath = "/Administration/ApplicantRegister";

    [Inject]
    public required ApplicantRegisterService Register { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Parameter]
    public Guid? Id { get; set; }

    private ApplicantRegisterEntryDTO? Entry;
    private ApplicantRegisterEntrySaveRequest? Request;
    private ConfirmDialog DeleteConfirm = default!;
    private bool Loading = true;
    private bool Busy;

    private bool IsNew => Id == null;

    private string ChapterIdText => Request?.ChapterId?.ToString() ?? "";

    protected override async Task OnParametersSetAsync() {
        Entry = null;
        Request = null;
        if (!AuthService.HasGlobalPermission(PermissionIdentifier.ManageApplicantRegister)) {
            Loading = false;
            return;
        }

        Loading = true;
        if (Id == null) {
            Request = new ApplicantRegisterEntrySaveRequest {
                OccurredOn = DateOnly.FromDateTime(DateTime.Today)
            };
        } else {
            Entry = await Register.GetAsync(Id.Value);
            Request = Entry == null ? null : ToRequest(Entry);
        }
        Loading = false;
    }

    private static ApplicantRegisterEntrySaveRequest ToRequest(ApplicantRegisterEntryDTO entry) {
        return new ApplicantRegisterEntrySaveRequest {
            Id = entry.Id,
            Kind = entry.Kind,
            FirstName = entry.FirstName,
            LastName = entry.LastName,
            DateOfBirth = entry.DateOfBirth,
            Street = entry.Street,
            HouseNumber = entry.HouseNumber,
            PostCode = entry.PostCode,
            City = entry.City,
            Email = entry.Email,
            OccurredOn = entry.OccurredOn,
            Note = entry.Note,
            ChapterId = entry.ChapterId
        };
    }

    private void OnChapterChanged(string value) {
        if (Request == null)
            return;
        Request.ChapterId = Guid.TryParse(value, out var chapterId) ? chapterId : null;
    }

    private async Task Save() {
        if (Request == null || Busy)
            return;

        Busy = true;
        Request.FirstName = Request.FirstName.Trim();
        Request.LastName = Request.LastName.Trim();
        Request.Street = Request.Street.Trim();
        Request.HouseNumber = Request.HouseNumber.Trim();
        Request.PostCode = Request.PostCode.Trim();
        Request.City = Request.City.Trim();
        Request.Email = string.IsNullOrWhiteSpace(Request.Email) ? null : Request.Email.Trim();
        Request.Note = string.IsNullOrWhiteSpace(Request.Note) ? null : Request.Note.Trim();

        var saved = Id == null
            ? await Register.CreateAsync(Request)
            : await Register.UpdateAsync(Id.Value, Request);
        Busy = false;
        if (saved == null)
            return;

        ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        Navigation.NavigateTo(ListPath);
    }

    private async Task Delete() {
        if (Id == null || Busy)
            return;

        var confirmed = await DeleteConfirm.ShowAsync();
        if (!confirmed)
            return;

        Busy = true;
        var deleted = await Register.DeleteAsync(Id.Value);
        Busy = false;
        if (!deleted)
            return;

        ToastService.ToastKey(I18nKey.Ui.ApplicantRegister.Deleted);
        Navigation.NavigateTo(ListPath);
    }
}
