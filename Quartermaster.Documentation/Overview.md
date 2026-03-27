# Quartermaster

A membership management and dues payment system for the Pirate Party (Piratenpartei). Handles member registration, dues calculation with flexible payment options, and organizational structure through chapters with role-based permissions.

## Tech Stack

- **.NET 10.0 / C#** with **ASP.NET Core**
- **FastEndpoints** — lightweight, strongly-typed endpoint routing (one endpoint per class)
- **LinqToDB** + **MySQL** — data access / ORM
- **FluentMigrator** — database schema versioning and migrations
- **Blazor WebAssembly** — client-side frontend with Bootstrap 5
- **Riok.Mapperly** — compile-time source-generated object mapping
- Custom token-based authentication + SAML SSO support

## Solution Structure

| Project | Role |
|---|---|
| `Quartermaster.Api` | Shared DTOs, request/response contracts, and permission identifiers |
| `Quartermaster.Data` | Entities, repositories, migrations, configuration settings |
| `Quartermaster.Server` | ASP.NET Core host, API endpoints, authentication handlers |
| `Quartermaster.Blazor` | Blazor WASM frontend — pages, components, services |
| `Quartermaster.Documentation` | Documentation and diagrams |

## Domain Features

### Users & Authentication
- Token-based login with SHA256-hashed tokens and browser fingerprint binding
- Token expiration with automatic extension
- SAML SSO support for third-party authentication
- PBKDF2 password hashing (SHA512, 500K iterations)

### Dues Selection
- Monthly pay group-based calculation
- 1% of yearly income calculation
- Underage special rate (12€)
- Reduced rates with justification (requires board approval workflow)
- Payment schedules: annual, quarterly, monthly (monthly requires direct deposit and yearly dues >= 36€)
- Direct deposit support with IBAN

### Chapters
- Organizational units representing regional branches
- Chapter-scoped officer roles

### Administrative Divisions
- Hierarchical German geographic data (countries → states → districts → cities)
- Loaded from bundled data files (~199K records)
- Linked to members via citizenship and address
- Associated with postal codes

### Permissions
- Global permissions (e.g. `users_create`, `chapters_create`)
- Chapter-scoped permissions (permission tied to a user + chapter)
- Enforced via FastEndpoints endpoint processors (`ChapterRequirement`, `ChapterPermissionRequirement`)

## Architecture

### API Layer (FastEndpoints)
Each endpoint is a self-contained class mapping to a single HTTP route. Example: `DueSelectionCreateEndpoint` → `POST /api/dueselector`. Public endpoints use `AllowAnonymous()`.

### Data Access (Repository Pattern)
Each entity has a dedicated repository inheriting from `RepositoryBase<T>`. The `DbContext` class (LinqToDB) registers all table mappings and repositories are injected via DI.

### Authentication Pipeline
1. Client sends `AuthToken` and `UserId` headers
2. `TokenAuthenticationHandler` validates the token hash against the database
3. Token scope checked (None, IP, BrowserFingerprint)
4. Claims principal constructed with user permissions

### Frontend (Blazor WASM)
- `AppStateService` singleton for client-side state management
- `ToastService` for UI notifications
- Reusable components: inputs (Checkbox, RadioGroup), navigation (NavMenuButton, CardButton), layout
- `EntryStateBase` provides common lifecycle for form pages

## Configuration

Settings are in `appsettings.json` / `appsettings.development.json`:

- `DatabaseSettings` — MySQL connection string
- `RootAccountSettings` — initial admin credentials
- `SamlSettings` — SAML endpoint configuration

In debug mode, the database schema is torn down and rebuilt on startup via FluentMigrator.

## Getting Started

1. Ensure MySQL is running locally
2. Configure the connection string in `appsettings.development.json`
3. Run the server project:
   ```
   dotnet run --project Quartermaster.Server
   ```
   The server will automatically run migrations and seed default data (admin account, permissions, administrative divisions).
4. The Blazor frontend is served from the same host
