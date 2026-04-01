# Error Handling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add consistent error handling: FastEndpoints global exception handler for server-side, Blazor ErrorBoundary + toast notifications for all API failures, and a configurable error contact message from the Options system.

**Architecture:** FastEndpoints' built-in exception handler returns structured error responses for unhandled exceptions. A new `general.error.contact` option stores who to contact on errors. A client config endpoint (`GET /api/config/client`) sends non-sensitive settings (like error contact) to the Blazor app on startup. ErrorBoundary wraps the Router in App.razor. All Blazor pages that make API calls get consistent try/catch with toast error notifications.

**Tech Stack:** FastEndpoints exception handler config, Blazor ErrorBoundary, existing ToastService, existing Options system

---

## File Structure

### New files

| File | Responsibility |
|---|---|
| `Quartermaster.Api/Config/ClientConfigDTO.cs` | DTO for client-side configuration (error contact, etc.) |
| `Quartermaster.Server/Config/ClientConfigEndpoint.cs` | `GET /api/config/client` returns client config |
| `Quartermaster.Blazor/Services/ClientConfigService.cs` | Fetches and caches client config on startup |
| `Quartermaster.Blazor/Components/AppErrorBoundary.razor` | Custom ErrorBoundary with error contact message and recovery |

### Modified files

| File | Change |
|---|---|
| `Quartermaster.Data/Options/OptionRepository.cs` | Add `general.error.contact` option definition |
| `Quartermaster.Server/Program.cs` | Configure FastEndpoints exception handler in `UseFastEndpoints()` |
| `Quartermaster.Blazor/App.razor` | Wrap Router in AppErrorBoundary |
| `Quartermaster.Blazor/Program.cs` | Register ClientConfigService |
| All Blazor pages with API calls (~15 files) | Add consistent try/catch with toast error notifications |

---

## Tasks

### Task 1: Error contact option + client config endpoint

**Files:**
- Modify: `Quartermaster.Data/Options/OptionRepository.cs`
- Create: `Quartermaster.Api/Config/ClientConfigDTO.cs`
- Create: `Quartermaster.Server/Config/ClientConfigEndpoint.cs`

- [ ] **Step 1: Add error contact option definition**

In `Quartermaster.Data/Options/OptionRepository.cs`, add at the end of `SupplementDefaults()` before the closing `}`:

```csharp
        AddDefinitionIfNotExists("general.error.contact",
            "Fehlerkontakt (wird bei Fehlern angezeigt)",
            OptionDataType.String, true, "", "Bei Problemen wende dich bitte an den Vorstand deiner Gliederung.");
```

- [ ] **Step 2: Create ClientConfigDTO**

Create `Quartermaster.Api/Config/ClientConfigDTO.cs`:

```csharp
namespace Quartermaster.Api.Config;

public class ClientConfigDTO {
    public string ErrorContact { get; set; } = "";
}
```

- [ ] **Step 3: Create ClientConfigEndpoint**

Create `Quartermaster.Server/Config/ClientConfigEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Config;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Config;

public class ClientConfigEndpoint : EndpointWithoutRequest<ClientConfigDTO> {
    private readonly OptionRepository _optionRepo;

    public ClientConfigEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Get("/api/config/client");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var errorContact = _optionRepo.GetGlobalValue("general.error.contact")?.Value ?? "";

        await SendAsync(new ClientConfigDTO {
            ErrorContact = errorContact
        }, cancellation: ct);
    }
}
```

- [ ] **Step 4: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

---

### Task 2: Configure FastEndpoints global exception handler

**Files:**
- Modify: `Quartermaster.Server/Program.cs`

- [ ] **Step 1: Configure exception handler in UseFastEndpoints**

In `Quartermaster.Server/Program.cs`, change:

```csharp
app.UseFastEndpoints();
```

To:

```csharp
app.UseFastEndpoints(c => {
    c.Errors.ResponseBuilder = (failures, ctx, statusCode) => {
        return new {
            statusCode,
            errors = failures.Select(f => new { field = f.PropertyName, message = f.ErrorMessage })
        };
    };
    c.Errors.UseProblemDetails();
});
```

Note: `UseProblemDetails()` enables RFC 7807 problem details format for validation errors. The `ResponseBuilder` provides a fallback for custom error shapes.

Actually, the simplest approach is just `UseProblemDetails()`:

```csharp
app.UseFastEndpoints(c => {
    c.Errors.UseProblemDetails();
});
```

This gives structured RFC 7807 responses for validation errors and `ThrowError` calls.

For unhandled exceptions, add `UseExceptionHandler` BEFORE `UseFastEndpoints`:

```csharp
app.UseExceptionHandler(appError => {
    appError.Run(async context => {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new {
            statusCode = 500,
            message = "Ein interner Serverfehler ist aufgetreten."
        });
    });
});
```

Place this right after `app.UseHttpsRedirection();` and before `app.UseRouting();`.

- [ ] **Step 2: Verify build and tests**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

---

### Task 3: Blazor ErrorBoundary + ClientConfigService

**Files:**
- Create: `Quartermaster.Blazor/Services/ClientConfigService.cs`
- Create: `Quartermaster.Blazor/Components/AppErrorBoundary.razor`
- Modify: `Quartermaster.Blazor/App.razor`
- Modify: `Quartermaster.Blazor/Program.cs`

- [ ] **Step 1: Create ClientConfigService**

Create `Quartermaster.Blazor/Services/ClientConfigService.cs`:

```csharp
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Config;

namespace Quartermaster.Blazor.Services;

public class ClientConfigService {
    private readonly HttpClient _http;
    private ClientConfigDTO? _config;

    public ClientConfigService(HttpClient http) {
        _http = http;
    }

    public string ErrorContact => _config?.ErrorContact ?? "";

    public async Task LoadAsync() {
        try {
            _config = await _http.GetFromJsonAsync<ClientConfigDTO>("/api/config/client");
        } catch {
            _config = new ClientConfigDTO();
        }
    }
}
```

- [ ] **Step 2: Register ClientConfigService in Blazor Program.cs**

In `Quartermaster.Blazor/Program.cs`, add after the HttpClient registration:

```csharp
builder.Services.AddScoped<Quartermaster.Blazor.Services.ClientConfigService>();
```

- [ ] **Step 3: Create AppErrorBoundary component**

Create `Quartermaster.Blazor/Components/AppErrorBoundary.razor`:

```razor
@inherits ErrorBoundary
@inject Quartermaster.Blazor.Services.ClientConfigService ConfigService

@if (CurrentException is not null) {
    <div class="container mt-4">
        <div class="alert alert-danger">
            <h4><i class="bi bi-exclamation-triangle"></i> Ein unerwarteter Fehler ist aufgetreten</h4>
            <p>Die Anwendung hat einen Fehler festgestellt. Bitte lade die Seite neu.</p>
            @if (!string.IsNullOrEmpty(ConfigService.ErrorContact)) {
                <p class="mb-2">@ConfigService.ErrorContact</p>
            }
            <button class="btn btn-outline-danger" @onclick="Recover">
                <i class="bi bi-arrow-clockwise"></i> Seite wiederherstellen
            </button>
        </div>
    </div>
} else {
    @ChildContent
}
```

- [ ] **Step 4: Wrap Router in App.razor with ErrorBoundary**

Read `Quartermaster.Blazor/App.razor` first, then wrap the `<Router>` component.

The current App.razor has a `<Router>` component. Wrap it:

```razor
<AppErrorBoundary>
    <Router AppAssembly="@typeof(App).Assembly">
        ...existing content...
    </Router>
</AppErrorBoundary>
```

Add `@using Quartermaster.Blazor.Components` at the top if not already present.

- [ ] **Step 5: Load ClientConfig on app startup**

In `Quartermaster.Blazor/App.razor`, add initialization. The simplest approach: add `@code` block that loads config:

Actually, better to load it in `MainLayout.razor.cs` or a dedicated initialization component. The simplest approach: load in `MainLayout` on first render.

In `Quartermaster.Blazor/Layout/MainLayout.razor`, read the file first. Add at the top:

```razor
@inject Quartermaster.Blazor.Services.ClientConfigService ConfigService
```

And add `@code` block (or modify existing code-behind):

```csharp
protected override async Task OnInitializedAsync() {
    await ConfigService.LoadAsync();
}
```

If MainLayout already has `OnInitializedAsync`, add `await ConfigService.LoadAsync();` to it.

- [ ] **Step 6: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

---

### Task 4: Fix silent API failures across all Blazor pages

**Files to modify:** All Blazor pages that make HTTP calls and either silently swallow errors or don't show user-facing error messages. Each page should:
1. Wrap API calls in try/catch
2. On `HttpRequestException` or non-success status: show toast with "Es ist ein Fehler aufgetreten."
3. On `Exception`: show toast with generic error message

The pages that need fixing (from the exploration):

**Silent failures (catch but no toast):**
- `Quartermaster.Blazor/Pages/Administration/MemberDetail.razor.cs` — catches HttpRequestException silently
- `Quartermaster.Blazor/Pages/Administration/EventDetail.razor.cs` — catches HttpRequestException silently in LoadEvent()
- `Quartermaster.Blazor/Pages/Administration/MotionDetail.razor.cs` — catches HttpRequestException silently
- `Quartermaster.Blazor/Pages/Administration/EventCreateFromTemplate.razor.cs` — catches HttpRequestException silently

**No error handling at all:**
- `Quartermaster.Blazor/Pages/Administration/OptionDetail.razor.cs` — no try/catch on HTTP calls

**Pattern to apply:** For each file, read it first, find the API call patterns, and add toast error notifications. Use:

```csharp
catch (HttpRequestException) {
    ToastService.Toast("Es ist ein Fehler aufgetreten.", "danger");
}
catch (Exception) {
    ToastService.Toast("Es ist ein Fehler aufgetreten.", "danger");
}
```

For pages that check `IsSuccessStatusCode` but don't show errors on failure, add:

```csharp
if (!response.IsSuccessStatusCode) {
    ToastService.Toast("Es ist ein Fehler aufgetreten.", "danger");
    return;
}
```

**Important:** Read each file before modifying. Don't add duplicate error handling where it already exists. Only add where missing.

- [ ] **Step 1: Fix MemberDetail.razor.cs**
- [ ] **Step 2: Fix EventDetail.razor.cs**
- [ ] **Step 3: Fix MotionDetail.razor.cs**
- [ ] **Step 4: Fix EventCreateFromTemplate.razor.cs**
- [ ] **Step 5: Fix OptionDetail.razor.cs**
- [ ] **Step 6: Scan for any other pages with missing error handling and fix**
- [ ] **Step 7: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

---

### Task 5: Final verification

- [ ] **Step 1: Run all tests**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

- [ ] **Step 2: Restart server and verify in Chrome**

Drop DB, restart, generate test data, navigate around to verify no errors:

```bash
mysql -u root -e "DROP DATABASE IF EXISTS quartermaster; CREATE DATABASE quartermaster;"
pkill -f "Quartermaster.Server"
nohup /usr/lib/dotnet/dotnet run --project Quartermaster.Server/Quartermaster.Server.csproj > /tmp/quartermaster-server.log 2>&1 &
```

Verify: App loads, test data generates, pages work, no console errors.

---

## Checklist: Production Readiness TODOs Covered

| TODO | Status |
|---|---|
| Add global exception handling middleware | ✅ UseExceptionHandler + FastEndpoints UseProblemDetails |
| Return proper HTTP status codes consistently | ✅ Already good (endpoints use 400/404); now 500s have structured responses |
| Add Blazor ErrorBoundary component | ✅ AppErrorBoundary wrapping Router with recovery button + error contact |
| Add user-facing error messages for API failures | ✅ Toast notifications on all API failures across all Blazor pages |
