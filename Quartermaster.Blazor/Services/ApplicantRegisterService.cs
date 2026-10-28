using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.ApplicantRegister;

namespace Quartermaster.Blazor.Services;

/// <summary>Calls of the register of rejected applicants; failed calls show their error as a toast and return null or false.</summary>
public class ApplicantRegisterService {
    private const string Base = "/api/admin/applicant-register";

    private readonly HttpClient _http;
    private readonly ToastService _toasts;

    public ApplicantRegisterService(HttpClient http, ToastService toasts) {
        _http = http;
        _toasts = toasts;
    }

    public Task<ApplicantRegisterEntrySearchResponse?> SearchAsync(string? query, int page, int pageSize) {
        var url = $"{Base}?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(query))
            url += $"&query={Uri.EscapeDataString(query)}";
        return GetAsync<ApplicantRegisterEntrySearchResponse>(url);
    }

    public Task<ApplicantRegisterEntryDTO?> GetAsync(Guid id) {
        return GetAsync<ApplicantRegisterEntryDTO>($"{Base}/{id}");
    }

    public Task<ApplicantRegisterEntryDTO?> CreateAsync(ApplicantRegisterEntrySaveRequest request) {
        return SendAsync<ApplicantRegisterEntryDTO>(_http.PostAsJsonAsync(Base, request));
    }

    public Task<ApplicantRegisterEntryDTO?> UpdateAsync(Guid id, ApplicantRegisterEntrySaveRequest request) {
        return SendAsync<ApplicantRegisterEntryDTO>(_http.PutAsJsonAsync($"{Base}/{id}", request));
    }

    public Task<bool> DeleteAsync(Guid id) {
        return SendAsync(_http.DeleteAsync($"{Base}/{id}"));
    }

    public Task<bool> DismissHitAsync(Guid applicationId, Guid hitId) {
        return SendAsync(_http.PostAsJsonAsync($"/api/admin/membershipapplications/{applicationId}/register-hits/{hitId}/dismiss", new { }));
    }

    private async Task<T?> GetAsync<T>(string url) where T : class {
        try {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) {
                await _toasts.ErrorAsync(response);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        } catch (HttpRequestException ex) {
            _toasts.Error(ex);
            return null;
        }
    }

    private async Task<T?> SendAsync<T>(Task<HttpResponseMessage> call) where T : class {
        try {
            var response = await call;
            if (!response.IsSuccessStatusCode) {
                await _toasts.ErrorAsync(response);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        } catch (HttpRequestException ex) {
            _toasts.Error(ex);
            return null;
        }
    }

    private async Task<bool> SendAsync(Task<HttpResponseMessage> call) {
        try {
            var response = await call;
            if (!response.IsSuccessStatusCode) {
                await _toasts.ErrorAsync(response);
                return false;
            }
            return true;
        } catch (HttpRequestException ex) {
            _toasts.Error(ex);
            return false;
        }
    }
}
