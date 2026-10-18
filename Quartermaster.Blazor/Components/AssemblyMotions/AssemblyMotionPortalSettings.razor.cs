using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionPortalSettings {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    [Parameter, EditorRequired]
    public Guid ChapterId { get; set; }

    private AssemblyMotionPortalSettingsDTO? Settings;
    private AssemblyMotionConnectionCheckDTO? ConnectionCheck;
    private AssemblyMotionConnectionCheckDTO? OpenSlidesConnectionCheck;
    private TypeEdit? Editing;
    private ConfirmDialog DeleteTypeDialog = default!;
    private bool Loading = true;
    private bool Forbidden;
    private bool Busy;

    private string EnableOpensAt = "";
    private string EnableClosesAt = "";

    private string OpensAt = "";
    private string ClosesAt = "";
    private string IntroMarkdown = "";
    private bool PrioritizationEnabled;
    private string PrioritizationStartsAt = "";
    private string PrioritizationEndsAt = "";
    private bool WikiMirrorEnabled;
    private string WikiPagePrefix = "";
    private string WikiAssemblyLabel = "";
    private int? OpenSlidesMeetingId;

    private bool CanEdit => AuthService.HasPermission(ChapterId, PermissionIdentifier.EditEvents);

    private TypeEdit EditModel => Editing!;

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Loading = true;
        try {
            var byEvent = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            if (!byEvent.IsSuccessStatusCode) {
                Settings = null;
            } else {
                var portal = await byEvent.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>();
                var response = await Http.GetAsync($"/api/assemblymotions/portals/{portal!.Id}/settings");
                if (response.IsSuccessStatusCode) {
                    Apply(await response.Content.ReadFromJsonAsync<AssemblyMotionPortalSettingsDTO>());
                } else {
                    Forbidden = true;
                }
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private void Apply(AssemblyMotionPortalSettingsDTO? settings) {
        Settings = settings;
        if (settings == null)
            return;

        OpensAt = AssemblyMotionDisplay.ToInput(settings.OpensAt);
        ClosesAt = AssemblyMotionDisplay.ToInput(settings.ClosesAt);
        IntroMarkdown = settings.IntroMarkdown;
        PrioritizationEnabled = settings.PrioritizationEnabled;
        PrioritizationStartsAt = AssemblyMotionDisplay.ToInput(settings.PrioritizationStartsAt);
        PrioritizationEndsAt = AssemblyMotionDisplay.ToInput(settings.PrioritizationEndsAt);
        WikiMirrorEnabled = settings.WikiMirrorEnabled;
        WikiPagePrefix = settings.WikiPagePrefix ?? "";
        WikiAssemblyLabel = settings.WikiAssemblyLabel ?? "";
        OpenSlidesMeetingId = settings.OpenSlidesMeetingId;
    }

    private async Task EnableAsync() {
        var opensAt = AssemblyMotionDisplay.FromInput(EnableOpensAt);
        var closesAt = AssemblyMotionDisplay.FromInput(EnableClosesAt);
        if (opensAt == null || closesAt == null) {
            ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.DatesRequired, "danger");
            return;
        }

        Busy = true;
        try {
            var response = await Http.PostAsJsonAsync("/api/assemblymotions/portals", new AssemblyMotionPortalEnableRequest {
                EventId = EventId,
                OpensAt = opensAt.Value,
                ClosesAt = closesAt.Value
            });
            if (response.IsSuccessStatusCode) {
                Apply(await response.Content.ReadFromJsonAsync<AssemblyMotionPortalSettingsDTO>());
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastPortalEnabled);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task SavePortalAsync() {
        var opensAt = AssemblyMotionDisplay.FromInput(OpensAt);
        var closesAt = AssemblyMotionDisplay.FromInput(ClosesAt);
        if (Settings == null || opensAt == null || closesAt == null) {
            ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.DatesRequired, "danger");
            return;
        }

        Busy = true;
        ConnectionCheck = null;
        OpenSlidesConnectionCheck = null;
        try {
            var response = await Http.PutAsJsonAsync($"/api/assemblymotions/portals/{Settings.Id}", new AssemblyMotionPortalUpdateRequest {
                Id = Settings.Id,
                OpensAt = opensAt.Value,
                ClosesAt = closesAt.Value,
                IntroMarkdown = IntroMarkdown,
                PrioritizationEnabled = PrioritizationEnabled,
                PrioritizationStartsAt = PrioritizationEnabled ? AssemblyMotionDisplay.FromInput(PrioritizationStartsAt) : null,
                PrioritizationEndsAt = PrioritizationEnabled ? AssemblyMotionDisplay.FromInput(PrioritizationEndsAt) : null,
                WikiMirrorEnabled = WikiMirrorEnabled,
                WikiPagePrefix = WikiMirrorEnabled ? WikiPagePrefix : null,
                WikiAssemblyLabel = WikiMirrorEnabled ? WikiAssemblyLabel : null,
                OpenSlidesMeetingId = OpenSlidesMeetingId
            });
            if (response.IsSuccessStatusCode) {
                Apply(await response.Content.ReadFromJsonAsync<AssemblyMotionPortalSettingsDTO>());
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastPortalSaved);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task CheckWikiConnectionAsync() {
        if (Settings == null)
            return;

        Busy = true;
        ConnectionCheck = null;
        try {
            var response = await Http.PostAsJsonAsync($"/api/assemblymotions/portals/{Settings.Id}/wiki/validate", new { });
            if (response.IsSuccessStatusCode) {
                ConnectionCheck = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();
            } else {
                await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task CheckOpenSlidesConnectionAsync() {
        if (Settings == null)
            return;

        Busy = true;
        OpenSlidesConnectionCheck = null;
        try {
            var response = await Http.PostAsJsonAsync($"/api/assemblymotions/portals/{Settings.Id}/openslides/validate", new { });
            if (response.IsSuccessStatusCode) {
                OpenSlidesConnectionCheck = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();
            } else {
                await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private void BeginAddType() {
        var nextOrder = Settings == null || Settings.Types.Count == 0 ? 1 : Settings.Types.Max(t => t.SortOrder) + 1;
        Editing = new TypeEdit {
            SortOrder = nextOrder
        };
    }

    private void BeginEditType(AssemblyMotionTypeSettingsDTO type) {
        Editing = new TypeEdit {
            Id = type.Id,
            Code = type.Code,
            Name = type.Name,
            SortOrder = type.SortOrder,
            MinSubmitters = type.MinSubmitters,
            Deadline = AssemblyMotionDisplay.ToInput(type.Deadline),
            CompetingDeadline = AssemblyMotionDisplay.ToInput(type.CompetingDeadline),
            Majority = (int)type.RequiredMajority,
            WikiTemplate = type.WikiTemplate,
            HasMotions = type.HasMotions
        };
    }

    private void CancelTypeEdit() {
        Editing = null;
    }

    private async Task SaveTypeAsync() {
        if (Settings == null || Editing == null)
            return;

        Busy = true;
        try {
            var response = Editing.Id == null
                ? await Http.PostAsJsonAsync($"/api/assemblymotions/portals/{Settings.Id}/types", Editing.ToCreateRequest(Settings.Id))
                : await Http.PutAsJsonAsync($"/api/assemblymotions/types/{Editing.Id}", Editing.ToUpdateRequest());
            if (response.IsSuccessStatusCode) {
                Editing = null;
                await RefreshTypesAsync();
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastTypeSaved);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task DeleteTypeAsync(AssemblyMotionTypeSettingsDTO type) {
        if (!await DeleteTypeDialog.ShowAsync(I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.DeleteTypeMessage, ("name", type.Name))]))
            return;

        Busy = true;
        try {
            var response = await Http.DeleteAsync($"/api/assemblymotions/types/{type.Id}");
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent) {
                await RefreshTypesAsync();
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastTypeDeleted);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task RefreshTypesAsync() {
        if (Settings == null)
            return;

        var fresh = await Http.GetFromJsonAsync<AssemblyMotionPortalSettingsDTO>($"/api/assemblymotions/portals/{Settings.Id}/settings");
        if (fresh != null)
            Settings.Types = fresh.Types;
    }

    private sealed class TypeEdit {
        public Guid? Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int SortOrder { get; set; }
        public int MinSubmitters { get; set; } = 1;
        public string Deadline { get; set; } = "";
        public string CompetingDeadline { get; set; } = "";
        public int Majority { get; set; }
        public string WikiTemplate { get; set; } = "";
        public bool HasMotions { get; set; }

        public AssemblyMotionTypeCreateRequest ToCreateRequest(Guid portalId) {
            var request = new AssemblyMotionTypeCreateRequest {
                PortalId = portalId
            };
            Fill(request);
            return request;
        }

        public AssemblyMotionTypeUpdateRequest ToUpdateRequest() {
            var request = new AssemblyMotionTypeUpdateRequest {
                Id = Id!.Value
            };
            Fill(request);
            return request;
        }

        private void Fill(AssemblyMotionTypeFields fields) {
            fields.Code = Code.Trim();
            fields.Name = Name.Trim();
            fields.SortOrder = SortOrder;
            fields.MinSubmitters = MinSubmitters;
            fields.Deadline = AssemblyMotionDisplay.FromInput(Deadline);
            fields.CompetingDeadline = AssemblyMotionDisplay.FromInput(CompetingDeadline);
            fields.RequiredMajority = (RequiredMajority)Majority;
            fields.WikiTemplate = WikiTemplate;
        }
    }
}
