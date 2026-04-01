# Configuration & Secrets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the application production-ready by: (1) creating a CLI command for initial admin setup, (2) moving SAML config to the database Options system, (3) making root account seeding DEBUG-only, (4) providing a config template for the only remaining secret — the database connection string.

**Architecture:** The server's `Main()` method checks for CLI arguments (`init-admin`) before starting the web host. SAML settings become OptionDefinitions in the existing Options system. RootAccountSettings auto-seeding is wrapped in `#if DEBUG`. A tracked `appsettings.template.json` documents the required structure.

**Tech Stack:** ASP.NET Core CLI args, existing Options system (OptionRepository/OptionDefinition), `#if DEBUG` compiler directives

---

## File Structure

### New files

| File | Responsibility |
|---|---|
| `Quartermaster.Server/Cli/AdminInitCommand.cs` | CLI command: prompts for username/password, creates admin user with permissions |
| `Quartermaster.Server/appsettings.template.json` | Git-tracked template showing required config structure (connection string only) |

### Modified files

| File | Change |
|---|---|
| `Quartermaster.Server/Program.cs` | Route CLI args to AdminInitCommand before web host; wrap root account seeding in `#if DEBUG`; remove SamlSettings Configure |
| `Quartermaster.Data/DbContext.cs` | Wrap `UserRepository.SupplementDefaults()` call in `#if DEBUG` |
| `Quartermaster.Data/Options/OptionRepository.cs` | Add SAML option definitions in `SupplementDefaults()` |
| `Quartermaster.Server/Users/SamlLoginStartEndpoint.cs` | Read SAML settings from OptionRepository instead of Config[] |
| `Quartermaster.Server/Users/SamlLoginConsumeEndpoint.cs` | Read SAML certificate from OptionRepository instead of Config[] |
| `Quartermaster.Data/RootAccountSettings.cs` | Make properties non-required (nullable) for optional config |
| `Quartermaster.Data/SamlSettings.cs` | Delete this file (settings move to Options system) |

---

## Tasks

### Task 1: Create appsettings.template.json

**Files:**
- Create: `Quartermaster.Server/appsettings.template.json`

- [ ] **Step 1: Create the template file**

Create `Quartermaster.Server/appsettings.template.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DatabaseSettings": {
    "ConnectionString": "server=<host>;user id=<user>;password=<password>;database=quartermaster;"
  }
}
```

This file is tracked in git (not in .gitignore). It documents the required configuration for production deployment. Developers copy it to `appsettings.json` or `appsettings.development.json` and fill in real values.

- [ ] **Step 2: Verify it's not gitignored**

Check `.gitignore` — it ignores `appsettings.json` and `appsettings.Development.json` but NOT `appsettings.template.json`.

- [ ] **Step 3: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

---

### Task 2: Create CLI admin init command

**Files:**
- Create: `Quartermaster.Server/Cli/AdminInitCommand.cs`
- Modify: `Quartermaster.Server/Program.cs`

**Context:** The server currently auto-seeds an admin user from `RootAccountSettings` in appsettings on every startup. For production, the admin should be created via a one-time CLI command: `dotnet run -- init-admin`. The command bootstraps the minimal services needed (DB, repositories), prompts for username and password via console, creates the user, grants permissions, and exits.

- [ ] **Step 1: Create AdminInitCommand**

Create `Quartermaster.Server/Cli/AdminInitCommand.cs`:

```csharp
using System;
using LinqToDB;
using LinqToDB.AspNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentMigrator.Runner;
using Quartermaster.Data;
using Quartermaster.Data.Migrations;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Cli;

public static class AdminInitCommand {
    public static int Execute(string[] args) {
        Console.Write("Admin-Benutzername: ");
        var username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username)) {
            Console.Error.WriteLine("Benutzername darf nicht leer sein.");
            return 1;
        }

        Console.Write("Admin-Passwort: ");
        var password = ReadPassword();
        if (string.IsNullOrEmpty(password) || password.Length < 12) {
            Console.Error.WriteLine("Passwort muss mindestens 12 Zeichen lang sein.");
            return 1;
        }

        Console.Write("Passwort wiederholen: ");
        var confirm = ReadPassword();
        if (password != confirm) {
            Console.Error.WriteLine("Passwörter stimmen nicht überein.");
            return 1;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connStr = config.GetValue<string>("DatabaseSettings:ConnectionString");
        if (string.IsNullOrEmpty(connStr)) {
            Console.Error.WriteLine("DatabaseSettings:ConnectionString ist nicht konfiguriert.");
            return 1;
        }

        var services = new ServiceCollection();
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                rb.AddMySql8()
                    .WithGlobalConnectionString(connStr)
                    .ScanIn(typeof(M001_InitialStructureMigration).Assembly).For.Migrations();
            });
        services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(connStr));
        DbContext.AddRepositories(services);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var migrator = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        migrator.MigrateUp();

        var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var existing = userRepo.GetByUsername(username);
        if (existing != null) {
            Console.Error.WriteLine($"Benutzer '{username}' existiert bereits.");
            return 1;
        }

        var settings = new RootAccountSettings { Username = username, Password = password };
        userRepo.SupplementDefaults(settings);

        Console.WriteLine($"Admin-Benutzer '{username}' wurde erstellt.");
        return 0;
    }

    private static string ReadPassword() {
        var password = "";
        while (true) {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0) {
                password = password[..^1];
                Console.Write("\b \b");
            } else if (!char.IsControl(key.KeyChar)) {
                password += key.KeyChar;
                Console.Write("*");
            }
        }
        return password;
    }
}
```

- [ ] **Step 2: Update Program.cs to route CLI commands**

In `Quartermaster.Server/Program.cs`, at the very beginning of `Main()`, before `var builder = ...`, add:

```csharp
if (args.Length > 0 && args[0] == "init-admin") {
    Environment.Exit(Quartermaster.Server.Cli.AdminInitCommand.Execute(args));
    return;
}
```

- [ ] **Step 3: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

---

### Task 3: Make root account seeding DEBUG-only

**Files:**
- Modify: `Quartermaster.Data/DbContext.cs`
- Modify: `Quartermaster.Data/RootAccountSettings.cs`
- Modify: `Quartermaster.Server/Program.cs`

**Context:** In DEBUG builds (development), the admin user is still auto-seeded from appsettings for convenience. In RELEASE builds (production), auto-seeding is skipped — admin must be created via `init-admin` CLI command.

- [ ] **Step 1: Make RootAccountSettings optional**

In `Quartermaster.Data/RootAccountSettings.cs`, change `required` to nullable:

```csharp
namespace Quartermaster.Data;

public class RootAccountSettings {
    public string? Username { get; set; }
    public string? Password { get; set; }
}
```

- [ ] **Step 2: Wrap root account seeding in #if DEBUG in DbContext.cs**

In `Quartermaster.Data/DbContext.cs`, wrap the UserRepository.SupplementDefaults call (lines 70-71):

Change:
```csharp
        scope.ServiceProvider.GetRequiredService<UserRepository>().SupplementDefaults(
            services.GetRequiredService<IOptions<RootAccountSettings>>().Value);
```

To:
```csharp
#if DEBUG
        var rootSettings = services.GetRequiredService<IOptions<RootAccountSettings>>().Value;
        if (!string.IsNullOrEmpty(rootSettings.Username) && !string.IsNullOrEmpty(rootSettings.Password)) {
            scope.ServiceProvider.GetRequiredService<UserRepository>().SupplementDefaults(rootSettings);
        }
#endif
```

- [ ] **Step 3: Guard UserRepository.SupplementDefaults against null**

In `Quartermaster.Data/Users/UserRepository.cs`, update the `SupplementDefaults` method signature:

Change:
```csharp
    public void SupplementDefaults(RootAccountSettings accountSettings) {
```

To:
```csharp
    public void SupplementDefaults(RootAccountSettings? accountSettings) {
        if (accountSettings == null || string.IsNullOrEmpty(accountSettings.Username) || string.IsNullOrEmpty(accountSettings.Password))
            return;
```

And update `AddRootAccount`:
```csharp
    private User AddRootAccount(RootAccountSettings accountSettings) {
        var rootUser = new User() {
            Username = accountSettings.Username!,
            PasswordHash = PasswordHashser.Hash(accountSettings.Password!)
        };
```

- [ ] **Step 4: Verify build in Debug**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj -c Debug
```

- [ ] **Step 5: Verify build in Release**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj -c Release
```

Both must succeed.

---

### Task 4: Move SAML settings to Options system

**Files:**
- Modify: `Quartermaster.Data/Options/OptionRepository.cs`
- Modify: `Quartermaster.Server/Users/SamlLoginStartEndpoint.cs`
- Modify: `Quartermaster.Server/Users/SamlLoginConsumeEndpoint.cs`
- Modify: `Quartermaster.Server/Program.cs`
- Delete: `Quartermaster.Data/SamlSettings.cs`

**Context:** SAML settings move from appsettings.json (file-based) to the Options system (database-stored). This allows admins to configure SAML from within the application UI. The three settings (`SamlEndpoint`, `SamlClientId`, `SamlCertificate`) become global, non-overridable OptionDefinitions. SAML endpoints read from OptionRepository and return 503 if not configured.

- [ ] **Step 1: Add SAML option definitions to OptionRepository.SupplementDefaults()**

In `Quartermaster.Data/Options/OptionRepository.cs`, add at the end of `SupplementDefaults()`, before the closing `}`:

```csharp
        AddDefinitionIfNotExists("auth.saml.endpoint",
            "SAML: Endpunkt-URL",
            OptionDataType.String, false, "", "");

        AddDefinitionIfNotExists("auth.saml.client_id",
            "SAML: Client-ID",
            OptionDataType.String, false, "", "");

        AddDefinitionIfNotExists("auth.saml.certificate",
            "SAML: Zertifikat (Base64, ohne Header/Footer)",
            OptionDataType.String, false, "", "");
```

- [ ] **Step 2: Update SamlLoginStartEndpoint to use OptionRepository**

Replace the entire content of `Quartermaster.Server/Users/SamlLoginStartEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Options;
using Saml;

namespace Quartermaster.Server.Users;

public class SamlLoginStartEndpoint : Endpoint<EmptyRequest> {
    private readonly OptionRepository _optionRepo;

    public SamlLoginStartEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Get("/api/users/SamlLoginStart");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct) {
        var clientId = _optionRepo.GetGlobalValue("auth.saml.client_id")?.Value;
        var endpoint = _optionRepo.GetGlobalValue("auth.saml.endpoint")?.Value;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(endpoint)) {
            await SendAsync(new { error = "SAML ist nicht konfiguriert." }, 503, ct);
            return;
        }

        var request = new AuthRequest(clientId, $"{BaseURL}api/users/SamlConsume");
        var url = request.GetRedirectUrl(endpoint);
        await SendRedirectAsync(url, allowRemoteRedirects: true);
    }
}
```

- [ ] **Step 3: Update SamlLoginConsumeEndpoint to use OptionRepository**

Replace the entire content of `Quartermaster.Server/Users/SamlLoginConsumeEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Users;

public class SamlLoginConsumeEndpoint : Endpoint<SamlLoginRequest, EmptyResponse> {
    private readonly OptionRepository _optionRepo;

    public SamlLoginConsumeEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Post("/api/users/SamlConsume");
        AllowAnonymous();
        AllowFormData(true);
        Description(x => x.Accepts<SamlLoginRequest>("application/x-www-form-urlencoded"));
    }

    public override async Task HandleAsync(SamlLoginRequest req, CancellationToken ct) {
        var certBase64 = _optionRepo.GetGlobalValue("auth.saml.certificate")?.Value;
        if (string.IsNullOrEmpty(certBase64)) {
            await SendAsync(new EmptyResponse(), 503, ct);
            return;
        }

        var cert = "-----BEGIN CERTIFICATE-----"
            + certBase64
            + "-----END CERTIFICATE-----";

        var samlResponse = new Saml.Response(cert, req.SamlData);
        await SendOkAsync(ct);
    }
}

public class SamlLoginRequest {
    [BindFrom("SAMLResponse")]
    public string? SamlData { get; set; }
}
```

- [ ] **Step 4: Remove SamlSettings from Program.cs**

In `Quartermaster.Server/Program.cs`, remove:

```csharp
builder.Services.Configure<SamlSettings>(builder.Configuration.GetSection("SamlSettings"));
```

And remove the `using Quartermaster.Data;` import if it was only needed for SamlSettings (check if other Data types are used — they likely are via DbContext, so the using probably stays).

- [ ] **Step 5: Delete SamlSettings.cs**

Delete `Quartermaster.Data/SamlSettings.cs`.

- [ ] **Step 6: Verify build and tests**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

---

### Task 5: Clean up Program.cs and verify

**Files:**
- Modify: `Quartermaster.Server/Program.cs`

- [ ] **Step 1: Remove RootAccountSettings Configure from release builds**

In `Quartermaster.Server/Program.cs`, wrap the RootAccountSettings configuration:

Change:
```csharp
builder.Services.Configure<RootAccountSettings>(builder.Configuration.GetSection("RootAccountSettings"));
```

To:
```csharp
#if DEBUG
builder.Services.Configure<RootAccountSettings>(builder.Configuration.GetSection("RootAccountSettings"));
#endif
```

- [ ] **Step 2: Verify the full server pipeline**

Build, run all tests, start server, verify in Chrome:

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Start server and verify app loads.

---

## Checklist: Production Readiness TODOs Covered

| TODO | Status |
|---|---|
| Remove hardcoded Admin/Admin default credentials from appsettings | ✅ Auto-seeding is `#if DEBUG` only; production uses `init-admin` CLI |
| Support environment variables for database connection string | ✅ CLI command uses `AddEnvironmentVariables()`; default builder also supports it |
| Support secrets management for production deployment | ✅ `appsettings.template.json` documents required config; only connection string needed in file |
| Make SAML settings configurable per environment | ✅ Moved to Options system — configurable from admin UI |
