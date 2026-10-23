using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Blazor.Services;

/// <summary>Calls of the UU-Assistent pages; failed calls show their error as a toast and return null or false.</summary>
public class SupportSignatureService {
    private const long FormReadLimitBytes = 64L * 1024 * 1024;
    private const string Base = "/api/support-signatures";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly ToastService _toasts;

    public SupportSignatureService(HttpClient http, IJSRuntime js, ToastService toasts) {
        _http = http;
        _js = js;
        _toasts = toasts;
    }

    public Task<List<SignatureCampaignDTO>?> CampaignsAsync(bool archived) {
        return GetAsync<List<SignatureCampaignDTO>>($"{Base}/campaigns?archived={archived.ToString().ToLowerInvariant()}");
    }

    public Task<SignatureCampaignDTO?> CampaignAsync(Guid id) {
        return GetAsync<SignatureCampaignDTO>($"{Base}/campaigns/{id}");
    }

    public Task<SignatureCampaignDTO?> CreateCampaignAsync(SignatureCampaignSaveRequest request) {
        return SendAsync<SignatureCampaignDTO>(_http.PostAsJsonAsync($"{Base}/campaigns", request));
    }

    public Task<SignatureCampaignDTO?> UpdateCampaignAsync(Guid id, SignatureCampaignSaveRequest request) {
        return SendAsync<SignatureCampaignDTO>(_http.PutAsJsonAsync($"{Base}/campaigns/{id}", request));
    }

    public Task<SignatureCampaignDTO?> ArchiveCampaignAsync(Guid id) {
        return SendAsync<SignatureCampaignDTO>(_http.PostAsJsonAsync($"{Base}/campaigns/{id}/archive", new { }));
    }

    public async Task<SignatureCampaignDTO?> UploadFormAsync(Guid campaignId, IBrowserFile file) {
        try {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream(FormReadLimitBytes);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "File", file.Name);
            return await SendAsync<SignatureCampaignDTO>(_http.PutAsync($"{Base}/campaigns/{campaignId}/form", content));
        } catch (IOException ex) {
            _toasts.Error(ex.Message);
            return null;
        }
    }

    public Task<bool> DeleteFormAsync(Guid campaignId) {
        return SendAsync(_http.DeleteAsync($"{Base}/campaigns/{campaignId}/form"));
    }

    public Task<SignatureOverviewDTO?> OverviewAsync(Guid campaignId) {
        return GetAsync<SignatureOverviewDTO>($"{Base}/campaigns/{campaignId}/overview");
    }

    public Task<List<SignatureIntakeDTO>?> IntakesAsync(Guid campaignId) {
        return GetAsync<List<SignatureIntakeDTO>>($"{Base}/campaigns/{campaignId}/intakes");
    }

    public Task<SignatureIntakeDTO?> AddIntakeAsync(Guid campaignId, SignatureIntakeSaveRequest request) {
        return SendAsync<SignatureIntakeDTO>(_http.PostAsJsonAsync($"{Base}/campaigns/{campaignId}/intakes", request));
    }

    public Task<SignatureIntakeDTO?> UpdateIntakeAsync(Guid intakeId, SignatureIntakeSaveRequest request) {
        return SendAsync<SignatureIntakeDTO>(_http.PutAsJsonAsync($"{Base}/intakes/{intakeId}", request));
    }

    public Task<bool> DeleteIntakeAsync(Guid intakeId) {
        return SendAsync(_http.DeleteAsync($"{Base}/intakes/{intakeId}"));
    }

    public Task<ShipmentPlanDTO?> PlanAsync(Guid campaignId) {
        return GetAsync<ShipmentPlanDTO>($"{Base}/campaigns/{campaignId}/shipment-plan");
    }

    public Task<List<SignatureShipmentDTO>?> ShipmentsAsync(Guid campaignId) {
        return GetAsync<List<SignatureShipmentDTO>>($"{Base}/campaigns/{campaignId}/shipments");
    }

    public Task<ShipmentPrepareResponse?> PrepareAsync(Guid campaignId, List<Guid> officeIds) {
        return SendAsync<ShipmentPrepareResponse>(_http.PostAsJsonAsync($"{Base}/campaigns/{campaignId}/shipments/prepare",
            new ShipmentPrepareRequest { OfficeIds = officeIds }));
    }

    public Task<ShipmentPrepareResponse?> CreateSingleAsync(Guid campaignId, Guid officeId, List<PlanLineInput> lines) {
        return SendAsync<ShipmentPrepareResponse>(_http.PostAsJsonAsync($"{Base}/campaigns/{campaignId}/shipments",
            new SingleShipmentRequest { OfficeId = officeId, Lines = lines }));
    }

    public Task<bool> ConfirmAsync(Guid shipmentId, DateOnly sentOn) {
        return SendAsync(_http.PostAsJsonAsync($"{Base}/shipments/{shipmentId}/confirm", new ShipmentConfirmRequest { SentOn = sentOn }));
    }

    public Task<bool> DiscardAsync(Guid shipmentId) {
        return SendAsync(_http.DeleteAsync($"{Base}/shipments/{shipmentId}"));
    }

    public Task<bool> RecordReturnAsync(Guid shipmentId, ReturnSaveRequest request) {
        return SendAsync(_http.PostAsJsonAsync($"{Base}/shipments/{shipmentId}/returns", request));
    }

    public Task<bool> UpdateReturnAsync(Guid returnId, ReturnSaveRequest request) {
        return SendAsync(_http.PutAsJsonAsync($"{Base}/returns/{returnId}", request));
    }

    public Task<bool> DeleteReturnAsync(Guid returnId) {
        return SendAsync(_http.DeleteAsync($"{Base}/returns/{returnId}"));
    }

    public async Task DownloadLettersAsync(Guid campaignId, IEnumerable<Guid> shipmentIds, string campaignName) {
        var query = string.Join("&", shipmentIds.Select(id => $"ids={id}"));
        await DownloadAsync($"{Base}/campaigns/{campaignId}/shipments/letters?{query}", $"Anschreiben-{campaignName}.pdf");
    }

    public Task<List<CertificationOfficeDTO>?> OfficesAsync() {
        return GetAsync<List<CertificationOfficeDTO>>($"{Base}/offices");
    }

    public Task<CertificationOfficeDTO?> CreateOfficeAsync(CertificationOfficeSaveRequest request) {
        return SendAsync<CertificationOfficeDTO>(_http.PostAsJsonAsync($"{Base}/offices", request));
    }

    public Task<CertificationOfficeDTO?> UpdateOfficeAsync(Guid officeId, CertificationOfficeSaveRequest request) {
        return SendAsync<CertificationOfficeDTO>(_http.PutAsJsonAsync($"{Base}/offices/{officeId}", request));
    }

    public Task<bool> DeleteOfficeAsync(Guid officeId) {
        return SendAsync(_http.DeleteAsync($"{Base}/offices/{officeId}"));
    }

    public Task<OfficeSuggestionDTO?> SuggestOfficeAsync(Guid municipalityId) {
        return GetAsync<OfficeSuggestionDTO>($"{Base}/offices/suggestion?municipalityId={municipalityId}");
    }

    /// <summary>The office forms of the municipality go to, or null (without a toast) when it has none.</summary>
    public async Task<CertificationOfficeDTO?> ResponsibleOfficeAsync(Guid municipalityId) {
        try {
            var response = await _http.GetAsync($"{Base}/offices/responsible?municipalityId={municipalityId}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode) {
                await _toasts.ErrorAsync(response);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<CertificationOfficeDTO>();
        } catch (HttpRequestException ex) {
            _toasts.Error(ex);
            return null;
        }
    }

    public Task<OfficeImportResultDTO?> ImportOfficesAsync(byte[] content, bool apply) {
        return SendAsync<OfficeImportResultDTO>(_http.PostAsJsonAsync($"{Base}/offices/import", new OfficeImportRequest { Content = content, Apply = apply }));
    }

    public Task<MemberSignatureCampaignDTO?> MemberCampaignAsync(Guid campaignId) {
        return GetAsync<MemberSignatureCampaignDTO>($"{Base}/member-campaigns/{campaignId}");
    }

    public Task DownloadCampaignFormAsync(SignatureCampaignDTO campaign) {
        return DownloadAsync($"{Base}/campaigns/{campaign.Id}/form", campaign.FormFileName ?? $"Formular-{campaign.Name}.pdf");
    }

    public Task DownloadMemberFormAsync(Guid campaignId, string campaignName) {
        return DownloadAsync($"{Base}/member-campaigns/{campaignId}/form", $"Formular-{campaignName}.pdf");
    }

    private async Task DownloadAsync(string url, string fileName) {
        try {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) {
                await _toasts.ErrorAsync(response);
                return;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await _js.InvokeVoidAsync("qmDownload.blob", bytes, fileName, "application/pdf");
        } catch (HttpRequestException ex) {
            _toasts.Error(ex);
        }
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
