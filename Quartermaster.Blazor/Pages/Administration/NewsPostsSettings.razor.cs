using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class NewsPostsSettings {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<NewsFeedDTO> feeds = [];
    private List<ProgrammeSourceDTO> sources = [];
    private NewsPostsStatusDTO? status;
    private string newSourceTitle = "";
    private bool syncing;
    private bool busy;

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
    }

    private async Task LoadAsync() {
        try {
            feeds = await Http.GetFromJsonAsync<List<NewsFeedDTO>>("/api/newsposts/feeds") ?? [];
            sources = await Http.GetFromJsonAsync<List<ProgrammeSourceDTO>>("/api/newsposts/sources") ?? [];
            status = await Http.GetFromJsonAsync<NewsPostsStatusDTO>("/api/newsposts/status");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void AddFeedRow() {
        feeds.Add(new NewsFeedDTO { IsEnabled = true });
    }

    private async Task SaveFeedAsync(NewsFeedDTO feed) {
        busy = true;
        try {
            var response = await Http.PutAsJsonAsync("/api/newsposts/feeds", feed);
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            await LoadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            busy = false;
        }
    }

    private async Task DeleteFeedAsync(NewsFeedDTO feed) {
        busy = true;
        try {
            var response = await Http.DeleteAsync($"/api/newsposts/feeds/{feed.Id}");
            if (!response.IsSuccessStatusCode)
                await ToastService.ErrorAsync(response);
            await LoadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            busy = false;
        }
    }

    private async Task AddSourceAsync() {
        busy = true;
        try {
            var response = await Http.PostAsJsonAsync("/api/newsposts/sources", new ProgrammeSourceCreateRequest { WikiPageTitle = newSourceTitle });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            newSourceTitle = "";
            await LoadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            busy = false;
        }
    }

    private async Task DeleteSourceAsync(ProgrammeSourceDTO source) {
        busy = true;
        try {
            var response = await Http.DeleteAsync($"/api/newsposts/sources/{source.Id}");
            if (!response.IsSuccessStatusCode)
                await ToastService.ErrorAsync(response);
            await LoadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            busy = false;
        }
    }

    private async Task SyncAsync() {
        syncing = true;
        try {
            var response = await Http.PostAsync("/api/newsposts/sources/sync", null);
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.NewsPosts.SyncDone);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        syncing = false;
        await LoadAsync();
    }

    private string YesNo(bool value) => value ? I18n[I18nKey.Ui.NewsPosts.Yes] : I18n[I18nKey.Ui.NewsPosts.No];
}
