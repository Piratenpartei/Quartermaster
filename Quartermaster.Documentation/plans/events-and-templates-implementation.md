# Event & Email System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Event management with typed checklists (text/motion/email), event templates with typed variables, client-side email preview, and stubbed email sending.

**Architecture:** Event and EventChecklistItem entities in Data layer. ChecklistItems use a type enum + JSON configuration for type-specific data. EventTemplate stores raw text with `{{variable}}` placeholders and typed variable definitions. Server handles checklist action execution (motion creation, email sending stub). TemplateRenderer + TemplateMockDataProvider moved to Api project for client-side preview. Blazor pages for full CRUD + checklist management.

**Tech Stack:** LinqToDB, FastEndpoints, Blazor WASM, Fluid.Core, Markdig, System.Text.Json.

---

## File Structure

### Data Layer (`Quartermaster.Data/`)
| File | Responsibility |
|------|---------------|
| `Events/Event.cs` | **Create** — Event entity |
| `Events/EventChecklistItem.cs` | **Create** — Checklist item entity with type enum |
| `Events/EventTemplate.cs` | **Create** — Event template entity |
| `Events/EventRepository.cs` | **Create** — CRUD for events, checklist items, templates |
| `Migrations/M001_InitialStructureMigration.cs` | **Modify** — Add Events, EventChecklistItems, EventTemplates tables |
| `DbContext.cs` | **Modify** — Add ITable properties, register repository |

### API Layer (`Quartermaster.Api/`)
| File | Responsibility |
|------|---------------|
| `Events/EventDTO.cs` | **Create** — List view DTO |
| `Events/EventDetailDTO.cs` | **Create** — Detail DTO with checklist items |
| `Events/EventChecklistItemDTO.cs` | **Create** — Checklist item DTO |
| `Events/EventCreateRequest.cs` | **Create** — Create event request |
| `Events/EventUpdateRequest.cs` | **Create** — Update event request |
| `Events/EventSearchRequest.cs` | **Create** — Search/filter request |
| `Events/EventSearchResponse.cs` | **Create** — Paginated response |
| `Events/ChecklistItemCreateRequest.cs` | **Create** — Add checklist item request |
| `Events/ChecklistItemUpdateRequest.cs` | **Create** — Update checklist item request |
| `Events/ChecklistItemCheckRequest.cs` | **Create** — Check item request with executeAction flag |
| `Events/EventTemplateDTO.cs` | **Create** — Template list DTO |
| `Events/EventTemplateDetailDTO.cs` | **Create** — Template detail with variables + checklist templates |
| `Events/EventTemplateCreateRequest.cs` | **Create** — Create template from event |
| `Events/EventFromTemplateRequest.cs` | **Create** — Create event from template with variable values |
| `Rendering/TemplateRenderer.cs` | **Create** — Shared Fluid+Markdig renderer (moved from Server) |
| `Rendering/TemplateMockDataProvider.cs` | **Create** — Shared mock data provider (moved from Server) |
| `Quartermaster.Api.csproj` | **Modify** — Add Fluid.Core and Markdig packages |

### Server Layer (`Quartermaster.Server/`)
| File | Responsibility |
|------|---------------|
| `Events/EventListEndpoint.cs` | **Create** — GET /api/events |
| `Events/EventCreateEndpoint.cs` | **Create** — POST /api/events |
| `Events/EventDetailEndpoint.cs` | **Create** — GET /api/events/{id} |
| `Events/EventUpdateEndpoint.cs` | **Create** — PUT /api/events/{id} |
| `Events/EventArchiveEndpoint.cs` | **Create** — POST /api/events/{id}/archive |
| `Events/ChecklistItemAddEndpoint.cs` | **Create** — POST /api/events/{id}/checklist |
| `Events/ChecklistItemUpdateEndpoint.cs` | **Create** — PUT /api/events/{id}/checklist/{itemId} |
| `Events/ChecklistItemDeleteEndpoint.cs` | **Create** — DELETE /api/events/{id}/checklist/{itemId} |
| `Events/ChecklistItemCheckEndpoint.cs` | **Create** — POST /api/events/{id}/checklist/{itemId}/check |
| `Events/ChecklistItemUncheckEndpoint.cs` | **Create** — POST /api/events/{id}/checklist/{itemId}/uncheck |
| `Events/EventTemplateListEndpoint.cs` | **Create** — GET /api/eventtemplates |
| `Events/EventTemplateCreateEndpoint.cs` | **Create** — POST /api/eventtemplates |
| `Events/EventTemplateDetailEndpoint.cs` | **Create** — GET /api/eventtemplates/{id} |
| `Events/EventTemplateDeleteEndpoint.cs` | **Create** — DELETE /api/eventtemplates/{id} |
| `Events/EventFromTemplateEndpoint.cs` | **Create** — POST /api/events/from-template |
| `Events/MemberEmailService.cs` | **Create** — Stubbed email sending service |
| `Events/ChecklistItemExecutor.cs` | **Create** — Executes checklist actions (motion creation, email sending) |
| `Options/TemplateRenderer.cs` | **Modify** — Redirect to shared Api version |
| `Options/TemplateMockDataProvider.cs` | **Modify** — Redirect to shared Api version |

### Blazor Layer (`Quartermaster.Blazor/`)
| File | Responsibility |
|------|---------------|
| `Pages/Administration/EventList.razor` + `.cs` | **Create** — Event list page |
| `Pages/Administration/EventDetail.razor` + `.cs` | **Create** — Event detail with checklist |
| `Pages/Administration/EventCreate.razor` + `.cs` | **Create** — Create event form |
| `Pages/Administration/EventCreateFromTemplate.razor` + `.cs` | **Create** — Create event from template |
| `Pages/Administration/EventTemplateList.razor` + `.cs` | **Create** — Template list page |
| `Pages/Administration/EventTemplateSave.razor` + `.cs` | **Create** — Save event as template |
| `Layout/MainLayout.razor` | **Modify** — Add nav links |

---

## Task 1: Entities + Migration + DbContext

**Files:**
- Create: `Quartermaster.Data/Events/Event.cs`
- Create: `Quartermaster.Data/Events/EventChecklistItem.cs`
- Create: `Quartermaster.Data/Events/EventTemplate.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`
- Modify: `Quartermaster.Data/DbContext.cs`

- [ ] **Step 1: Create Event entity**

Create `Quartermaster.Data/Events/Event.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Event {
    public const string TableName = "Events";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    public bool IsArchived { get; set; }
    public Guid? EventTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 2: Create EventChecklistItem entity**

Create `Quartermaster.Data/Events/EventChecklistItem.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class EventChecklistItem {
    public const string TableName = "EventChecklistItems";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public int SortOrder { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public string Label { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Configuration { get; set; }
    public Guid? ResultId { get; set; }
}

public enum ChecklistItemType {
    Text = 0,
    CreateMotion = 1,
    SendEmail = 2
}
```

- [ ] **Step 3: Create EventTemplate entity**

Create `Quartermaster.Data/Events/EventTemplate.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class EventTemplate {
    public const string TableName = "EventTemplates";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string PublicNameTemplate { get; set; } = "";
    public string? DescriptionTemplate { get; set; }
    public string Variables { get; set; } = "[]";
    public string ChecklistItemTemplates { get; set; } = "[]";
    public Guid? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 4: Add tables to migration**

In `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`, add `using Quartermaster.Data.Events;` to the imports.

In the `Up()` method, add before the closing `}` (after the MemberImportLog table creation):

```csharp
Create.Table(EventTemplate.TableName)
    .WithColumn(nameof(EventTemplate.Id)).AsGuid().PrimaryKey()
    .WithColumn(nameof(EventTemplate.Name)).AsString(512)
    .WithColumn(nameof(EventTemplate.PublicNameTemplate)).AsString(512)
    .WithColumn(nameof(EventTemplate.DescriptionTemplate)).AsString(8192).Nullable()
    .WithColumn(nameof(EventTemplate.Variables)).AsString(8192)
    .WithColumn(nameof(EventTemplate.ChecklistItemTemplates)).AsString(8192)
    .WithColumn(nameof(EventTemplate.ChapterId)).AsGuid().Nullable()
    .WithColumn(nameof(EventTemplate.CreatedAt)).AsDateTime();

Create.ForeignKey("FK_EventTemplates_ChapterId_Chapters_Id")
    .FromTable(EventTemplate.TableName).ForeignColumn(nameof(EventTemplate.ChapterId))
    .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

Create.Table(Event.TableName)
    .WithColumn(nameof(Event.Id)).AsGuid().PrimaryKey().Indexed()
    .WithColumn(nameof(Event.ChapterId)).AsGuid()
    .WithColumn(nameof(Event.InternalName)).AsString(512)
    .WithColumn(nameof(Event.PublicName)).AsString(512)
    .WithColumn(nameof(Event.Description)).AsString(8192).Nullable()
    .WithColumn(nameof(Event.EventDate)).AsDateTime().Nullable()
    .WithColumn(nameof(Event.IsArchived)).AsBoolean()
    .WithColumn(nameof(Event.EventTemplateId)).AsGuid().Nullable()
    .WithColumn(nameof(Event.CreatedAt)).AsDateTime();

Create.ForeignKey("FK_Events_ChapterId_Chapters_Id")
    .FromTable(Event.TableName).ForeignColumn(nameof(Event.ChapterId))
    .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

Create.ForeignKey("FK_Events_EventTemplateId_EventTemplates_Id")
    .FromTable(Event.TableName).ForeignColumn(nameof(Event.EventTemplateId))
    .ToTable(EventTemplate.TableName).PrimaryColumn(nameof(EventTemplate.Id));

Create.Table(EventChecklistItem.TableName)
    .WithColumn(nameof(EventChecklistItem.Id)).AsGuid().PrimaryKey()
    .WithColumn(nameof(EventChecklistItem.EventId)).AsGuid()
    .WithColumn(nameof(EventChecklistItem.SortOrder)).AsInt32()
    .WithColumn(nameof(EventChecklistItem.ItemType)).AsInt32()
    .WithColumn(nameof(EventChecklistItem.Label)).AsString(1024)
    .WithColumn(nameof(EventChecklistItem.IsCompleted)).AsBoolean()
    .WithColumn(nameof(EventChecklistItem.CompletedAt)).AsDateTime().Nullable()
    .WithColumn(nameof(EventChecklistItem.Configuration)).AsString(8192).Nullable()
    .WithColumn(nameof(EventChecklistItem.ResultId)).AsGuid().Nullable();

Create.ForeignKey("FK_EventChecklistItems_EventId_Events_Id")
    .FromTable(EventChecklistItem.TableName).ForeignColumn(nameof(EventChecklistItem.EventId))
    .ToTable(Event.TableName).PrimaryColumn(nameof(Event.Id));
```

In the `Down()` method, add three lines after `DisableForeignKeyChecks();` (before the existing DropTableIfExists calls):

```csharp
DropTableIfExists(EventChecklistItem.TableName);
DropTableIfExists(Event.TableName);
DropTableIfExists(EventTemplate.TableName);
```

- [ ] **Step 5: Update DbContext**

In `Quartermaster.Data/DbContext.cs`:

Add using: `using Quartermaster.Data.Events;`

Add ITable properties after the existing ones:
```csharp
public ITable<Event> Events => this.GetTable<Event>();
public ITable<EventChecklistItem> EventChecklistItems => this.GetTable<EventChecklistItem>();
public ITable<EventTemplate> EventTemplates => this.GetTable<EventTemplate>();
```

Add repository registration in `AddRepositories()`:
```csharp
services.AddScoped<EventRepository>();
```

(EventRepository will be created in the next task.)

- [ ] **Step 6: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Data/Quartermaster.Data.csproj`

Expected: Build may fail on missing EventRepository — that's fine. Or comment out the registration line temporarily.

---

## Task 2: EventRepository

**Files:**
- Create: `Quartermaster.Data/Events/EventRepository.cs`

- [ ] **Step 1: Create EventRepository**

Create `Quartermaster.Data/Events/EventRepository.cs`:

```csharp
using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Events;

public class EventRepository {
    private readonly DbContext _context;

    public EventRepository(DbContext context) {
        _context = context;
    }

    // Events
    public Event? Get(Guid id)
        => _context.Events.Where(e => e.Id == id).FirstOrDefault();

    public void Create(Event ev) => _context.Insert(ev);

    public void Update(Event ev) {
        _context.Events
            .Where(e => e.Id == ev.Id)
            .Set(e => e.InternalName, ev.InternalName)
            .Set(e => e.PublicName, ev.PublicName)
            .Set(e => e.Description, ev.Description)
            .Set(e => e.EventDate, ev.EventDate)
            .Update();
    }

    public void SetArchived(Guid id, bool archived) {
        _context.Events
            .Where(e => e.Id == id)
            .Set(e => e.IsArchived, archived)
            .Update();
    }

    public (List<Event> Items, int TotalCount) Search(Guid? chapterId, bool includeArchived, int page, int pageSize) {
        var q = _context.Events.AsQueryable();

        if (chapterId.HasValue)
            q = q.Where(e => e.ChapterId == chapterId.Value);

        if (!includeArchived)
            q = q.Where(e => !e.IsArchived);

        var totalCount = q.Count();
        var items = q.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    // Checklist Items
    public List<EventChecklistItem> GetChecklistItems(Guid eventId)
        => _context.EventChecklistItems
            .Where(i => i.EventId == eventId)
            .OrderBy(i => i.SortOrder)
            .ToList();

    public EventChecklistItem? GetChecklistItem(Guid itemId)
        => _context.EventChecklistItems.Where(i => i.Id == itemId).FirstOrDefault();

    public void CreateChecklistItem(EventChecklistItem item) => _context.Insert(item);

    public void UpdateChecklistItem(EventChecklistItem item) {
        _context.EventChecklistItems
            .Where(i => i.Id == item.Id)
            .Set(i => i.SortOrder, item.SortOrder)
            .Set(i => i.ItemType, item.ItemType)
            .Set(i => i.Label, item.Label)
            .Set(i => i.Configuration, item.Configuration)
            .Update();
    }

    public void DeleteChecklistItem(Guid itemId) {
        _context.EventChecklistItems.Where(i => i.Id == itemId).Delete();
    }

    public void CheckItem(Guid itemId, Guid? resultId) {
        _context.EventChecklistItems
            .Where(i => i.Id == itemId)
            .Set(i => i.IsCompleted, true)
            .Set(i => i.CompletedAt, DateTime.UtcNow)
            .Set(i => i.ResultId, resultId)
            .Update();
    }

    public void UncheckItem(Guid itemId) {
        _context.EventChecklistItems
            .Where(i => i.Id == itemId)
            .Set(i => i.IsCompleted, false)
            .Set(i => i.CompletedAt, (DateTime?)null)
            .Update();
    }

    // Templates
    public EventTemplate? GetTemplate(Guid id)
        => _context.EventTemplates.Where(t => t.Id == id).FirstOrDefault();

    public List<EventTemplate> GetAllTemplates()
        => _context.EventTemplates.OrderBy(t => t.Name).ToList();

    public void CreateTemplate(EventTemplate template) => _context.Insert(template);

    public void DeleteTemplate(Guid id) {
        _context.EventTemplates.Where(t => t.Id == id).Delete();
    }
}
```

- [ ] **Step 2: Uncomment EventRepository registration in DbContext if it was commented out.**

- [ ] **Step 3: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Data/Quartermaster.Data.csproj`

Expected: 0 errors.

---

## Task 3: API DTOs

**Files:**
- Create all files in `Quartermaster.Api/Events/`

- [ ] **Step 1: Create EventDTO**

Create `Quartermaster.Api/Events/EventDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public DateTime? EventDate { get; set; }
    public bool IsArchived { get; set; }
    public int ChecklistTotal { get; set; }
    public int ChecklistCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 2: Create EventChecklistItemDTO**

Create `Quartermaster.Api/Events/EventChecklistItemDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventChecklistItemDTO {
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public int ItemType { get; set; }
    public string Label { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Configuration { get; set; }
    public Guid? ResultId { get; set; }
}
```

- [ ] **Step 3: Create EventDetailDTO**

Create `Quartermaster.Api/Events/EventDetailDTO.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventDetailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    public bool IsArchived { get; set; }
    public Guid? EventTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<EventChecklistItemDTO> ChecklistItems { get; set; } = new();
}
```

- [ ] **Step 4: Create request/response DTOs**

Create `Quartermaster.Api/Events/EventCreateRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventCreateRequest {
    public Guid ChapterId { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
}
```

Create `Quartermaster.Api/Events/EventUpdateRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventUpdateRequest {
    public Guid Id { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
}
```

Create `Quartermaster.Api/Events/EventSearchRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventSearchRequest {
    public Guid? ChapterId { get; set; }
    public bool IncludeArchived { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
```

Create `Quartermaster.Api/Events/EventSearchResponse.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventSearchResponse {
    public List<EventDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
```

Create `Quartermaster.Api/Events/ChecklistItemCreateRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemCreateRequest {
    public Guid EventId { get; set; }
    public int SortOrder { get; set; }
    public int ItemType { get; set; }
    public string Label { get; set; } = "";
    public string? Configuration { get; set; }
}
```

Create `Quartermaster.Api/Events/ChecklistItemUpdateRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemUpdateRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public int SortOrder { get; set; }
    public int ItemType { get; set; }
    public string Label { get; set; } = "";
    public string? Configuration { get; set; }
}
```

Create `Quartermaster.Api/Events/ChecklistItemCheckRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemCheckRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public bool ExecuteAction { get; set; }
}
```

- [ ] **Step 5: Create template DTOs**

Create `Quartermaster.Api/Events/EventTemplateDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventTemplateDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int VariableCount { get; set; }
    public int ChecklistItemCount { get; set; }
    public Guid? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Create `Quartermaster.Api/Events/EventTemplateDetailDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventTemplateDetailDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string PublicNameTemplate { get; set; } = "";
    public string? DescriptionTemplate { get; set; }
    public string Variables { get; set; } = "[]";
    public string ChecklistItemTemplates { get; set; } = "[]";
    public Guid? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Create `Quartermaster.Api/Events/EventTemplateCreateRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Events;

public class EventTemplateCreateRequest {
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public string Variables { get; set; } = "[]";
}
```

Create `Quartermaster.Api/Events/EventFromTemplateRequest.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventFromTemplateRequest {
    public Guid TemplateId { get; set; }
    public Guid ChapterId { get; set; }
    public Dictionary<string, string> VariableValues { get; set; } = new();
}
```

- [ ] **Step 6: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Api/Quartermaster.Api.csproj`

Expected: 0 errors.

---

## Task 4: Shared TemplateRenderer + Packages

**Files:**
- Create: `Quartermaster.Api/Rendering/TemplateRenderer.cs`
- Create: `Quartermaster.Api/Rendering/TemplateMockDataProvider.cs`
- Modify: `Quartermaster.Api/Quartermaster.Api.csproj`
- Modify: `Quartermaster.Server/Options/TemplateRenderer.cs`
- Modify: `Quartermaster.Server/Options/TemplateMockDataProvider.cs`

- [ ] **Step 1: Add Fluid.Core and Markdig to Api project**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet add Quartermaster.Api/Quartermaster.Api.csproj package Fluid.Core --version 3.0.0-beta.5 && dotnet add Quartermaster.Api/Quartermaster.Api.csproj package Markdig`

- [ ] **Step 2: Create shared TemplateRenderer**

Create `Quartermaster.Api/Rendering/TemplateRenderer.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid;
using Markdig;

namespace Quartermaster.Api.Rendering;

public static class TemplateRenderer {
    private static readonly FluidParser Parser = new();
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static async Task<(string? Html, string? Error)> RenderAsync(
        string markdownTemplate, Dictionary<string, object> model) {

        if (!Parser.TryParse(markdownTemplate, out var template, out var error))
            return (null, $"Template parse error: {error}");

        var context = new TemplateContext();
        foreach (var (key, value) in model)
            context.SetValue(key, value);

        var rendered = await template.RenderAsync(context);
        var html = Markdown.ToHtml(rendered, MarkdownPipeline);
        return (html, null);
    }
}
```

- [ ] **Step 3: Create shared TemplateMockDataProvider**

Create `Quartermaster.Api/Rendering/TemplateMockDataProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Members;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Api.Rendering;

public static class TemplateMockDataProvider {
    public static Dictionary<string, object> GetMockData(string templateModels) {
        var data = new Dictionary<string, object>();
        var models = templateModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var model in models) {
            switch (model) {
                case "MembershipApplicationDetailDTO":
                    data["application"] = new MembershipApplicationDetailDTO {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        DateOfBirth = new DateTime(1990, 1, 15),
                        Citizenship = "Deutsch",
                        EMail = "max.mustermann@example.com",
                        PhoneNumber = "0170 1234567",
                        AddressStreet = "Musterstraße",
                        AddressHouseNbr = "42",
                        AddressPostCode = "10115",
                        AddressCity = "Berlin",
                        ChapterName = "Piratenpartei Berlin",
                        Status = 1,
                        SubmittedAt = DateTime.UtcNow.AddDays(-3),
                        EntryDate = DateTime.UtcNow
                    };
                    break;

                case "DueSelectionDetailDTO":
                    data["selection"] = new DueSelectionDetailDTO {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        EMail = "max.mustermann@example.com",
                        SelectedValuation = 4,
                        SelectedDue = 24,
                        ReducedAmount = 24,
                        ReducedJustification = "Student ohne Einkommen",
                        Status = 1
                    };
                    break;

                case "ChapterDTO":
                    data["chapter"] = new ChapterDTO {
                        Id = Guid.NewGuid(),
                        Name = "Piratenpartei Berlin"
                    };
                    break;

                case "MemberDetailDTO":
                    data["member"] = new MemberDetailDTO {
                        Id = Guid.NewGuid(),
                        MemberNumber = 12345,
                        FirstName = "Max",
                        LastName = "Mustermann",
                        EMail = "max.mustermann@example.com",
                        PostCode = "10115",
                        City = "Berlin",
                        Street = "Musterstraße 42",
                        Country = "DE",
                        DateOfBirth = new DateTime(1990, 1, 15),
                        Citizenship = "DE",
                        ChapterName = "Piratenpartei Berlin",
                        MembershipFee = 72m,
                        EntryDate = new DateTime(2020, 3, 1),
                        HasVotingRights = true,
                        LastImportedAt = DateTime.UtcNow
                    };
                    break;
            }
        }

        return data;
    }
}
```

- [ ] **Step 4: Update Server versions to delegate to shared ones**

Replace `Quartermaster.Server/Options/TemplateRenderer.cs` contents with:

```csharp
// Shared implementation moved to Quartermaster.Api.Rendering.TemplateRenderer
// This file kept for backward compatibility — server code should migrate to the shared version.
using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Options;

public static class TemplateRenderer {
    public static async System.Threading.Tasks.Task<(string? Html, string? Error)> RenderAsync(
        string markdownTemplate, System.Collections.Generic.Dictionary<string, object> model)
        => await Api.Rendering.TemplateRenderer.RenderAsync(markdownTemplate, model);
}
```

Replace `Quartermaster.Server/Options/TemplateMockDataProvider.cs` contents with:

```csharp
// Shared implementation moved to Quartermaster.Api.Rendering.TemplateMockDataProvider
using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Options;

public static class TemplateMockDataProvider {
    public static System.Collections.Generic.Dictionary<string, object> GetMockData(string templateModels)
        => Api.Rendering.TemplateMockDataProvider.GetMockData(templateModels);
}
```

- [ ] **Step 5: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Server/Quartermaster.Server.csproj`

Expected: 0 errors.

---

## Task 5: ChecklistItemExecutor + MemberEmailService

**Files:**
- Create: `Quartermaster.Server/Events/ChecklistItemExecutor.cs`
- Create: `Quartermaster.Server/Events/MemberEmailService.cs`
- Modify: `Quartermaster.Server/Program.cs`

- [ ] **Step 1: Create MemberEmailService (stubbed)**

Create `Quartermaster.Server/Events/MemberEmailService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Events;

public class MemberEmailService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberEmailService> _logger;

    public MemberEmailService(IServiceScopeFactory scopeFactory, ILogger<MemberEmailService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public (int RecipientCount, string? Error) SendEmail(string targetType, Guid targetId, string templateIdentifier) {
        using var scope = _scopeFactory.CreateScope();
        var memberRepo = scope.ServiceProvider.GetRequiredService<MemberRepository>();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();

        // Resolve recipients
        List<Member> recipients;
        if (targetType == "Chapter") {
            var chapterIds = chapterRepo.GetDescendantIds(targetId);
            var (members, _) = memberRepo.Search(null, null, 1, 100000);
            recipients = members.Where(m => m.ChapterId.HasValue && chapterIds.Contains(m.ChapterId.Value)).ToList();
        } else {
            var (members, _) = memberRepo.Search(null, null, 1, 100000);
            recipients = members.Where(m => m.ResidenceAdministrativeDivisionId == targetId).ToList();
        }

        recipients = recipients.Where(m => !string.IsNullOrEmpty(m.EMail)).ToList();

        if (recipients.Count == 0)
            return (0, "No recipients found");

        // Resolve template
        var templateValue = optionRepo.ResolveValue(templateIdentifier, null, chapterRepo);
        if (string.IsNullOrEmpty(templateValue))
            return (0, $"Template '{templateIdentifier}' not found or empty");

        // STUB: Log what would be sent instead of actually sending
        _logger.LogInformation(
            "EMAIL STUB: Would send email to {Count} recipients using template '{Template}'",
            recipients.Count, templateIdentifier);

        foreach (var recipient in recipients.Take(5)) {
            _logger.LogInformation("  → {Email} ({Name})", recipient.EMail, $"{recipient.FirstName} {recipient.LastName}");
        }

        if (recipients.Count > 5)
            _logger.LogInformation("  → ... and {Count} more", recipients.Count - 5);

        return (recipients.Count, null);
    }
}
```

- [ ] **Step 2: Create ChecklistItemExecutor**

Create `Quartermaster.Server/Events/ChecklistItemExecutor.cs`:

```csharp
using System;
using System.Text.Json;
using Markdig;
using Quartermaster.Data.Events;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Events;

public class ChecklistItemExecutor {
    private readonly MotionRepository _motionRepo;
    private readonly MemberEmailService _emailService;

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public ChecklistItemExecutor(MotionRepository motionRepo, MemberEmailService emailService) {
        _motionRepo = motionRepo;
        _emailService = emailService;
    }

    public (Guid? ResultId, string? Error) Execute(EventChecklistItem item) {
        return item.ItemType switch {
            ChecklistItemType.CreateMotion => ExecuteCreateMotion(item),
            ChecklistItemType.SendEmail => ExecuteSendEmail(item),
            _ => (null, null)
        };
    }

    private (Guid? ResultId, string? Error) ExecuteCreateMotion(EventChecklistItem item) {
        if (string.IsNullOrEmpty(item.Configuration))
            return (null, "No configuration for motion creation");

        var config = JsonSerializer.Deserialize<MotionConfig>(item.Configuration);
        if (config == null)
            return (null, "Invalid motion configuration");

        var motion = new Motion {
            ChapterId = config.ChapterId,
            AuthorName = "System (Event)",
            AuthorEMail = "",
            Title = config.MotionTitle,
            Text = Markdown.ToHtml(config.MotionText, MarkdownPipeline),
            IsPublic = false,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _motionRepo.Create(motion);
        return (motion.Id, null);
    }

    private (Guid? ResultId, string? Error) ExecuteSendEmail(EventChecklistItem item) {
        if (string.IsNullOrEmpty(item.Configuration))
            return (null, "No configuration for email sending");

        var config = JsonSerializer.Deserialize<EmailConfig>(item.Configuration);
        if (config == null)
            return (null, "Invalid email configuration");

        var (count, error) = _emailService.SendEmail(config.TargetType, config.TargetId, config.TemplateIdentifier);
        if (error != null)
            return (null, error);

        return (null, null);
    }

    private class MotionConfig {
        public Guid ChapterId { get; set; }
        public string MotionTitle { get; set; } = "";
        public string MotionText { get; set; } = "";
    }

    private class EmailConfig {
        public string TargetType { get; set; } = "";
        public Guid TargetId { get; set; }
        public string TemplateIdentifier { get; set; } = "";
    }
}
```

- [ ] **Step 3: Register services in Program.cs**

In `Quartermaster.Server/Program.cs`, add after the existing service registrations (after the MemberImportService/HostedService lines):

```csharp
builder.Services.AddSingleton<Quartermaster.Server.Events.MemberEmailService>();
builder.Services.AddScoped<Quartermaster.Server.Events.ChecklistItemExecutor>();
```

- [ ] **Step 4: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Server/Quartermaster.Server.csproj`

Expected: 0 errors.

---

## Task 6: Event CRUD Endpoints

**Files:**
- Create: `Quartermaster.Server/Events/EventListEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventCreateEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventDetailEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventUpdateEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventArchiveEndpoint.cs`

- [ ] **Step 1: Create EventListEndpoint**

Create `Quartermaster.Server/Events/EventListEndpoint.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventListEndpoint : Endpoint<EventSearchRequest, EventSearchResponse> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventListEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _eventRepo.Search(req.ChapterId, req.IncludeArchived, req.Page, req.PageSize);
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(e => {
            var checklistItems = _eventRepo.GetChecklistItems(e.Id);
            return new EventDTO {
                Id = e.Id,
                ChapterId = e.ChapterId,
                ChapterName = chapters.TryGetValue(e.ChapterId, out var name) ? name : "",
                PublicName = e.PublicName,
                EventDate = e.EventDate,
                IsArchived = e.IsArchived,
                ChecklistTotal = checklistItems.Count,
                ChecklistCompleted = checklistItems.Count(i => i.IsCompleted),
                CreatedAt = e.CreatedAt
            };
        }).ToList();

        await SendAsync(new EventSearchResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
```

- [ ] **Step 2: Create EventCreateEndpoint**

Create `Quartermaster.Server/Events/EventCreateEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventCreateEndpoint : Endpoint<EventCreateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly Data.Chapters.ChapterRepository _chapterRepo;

    public EventCreateEndpoint(EventRepository eventRepo, Data.Chapters.ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventCreateRequest req, CancellationToken ct) {
        var ev = new Event {
            ChapterId = req.ChapterId,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.Create(ev);

        var chapter = _chapterRepo.Get(ev.ChapterId);

        await SendAsync(new EventDetailDTO {
            Id = ev.Id,
            ChapterId = ev.ChapterId,
            ChapterName = chapter?.Name ?? "",
            InternalName = ev.InternalName,
            PublicName = ev.PublicName,
            Description = ev.Description,
            EventDate = ev.EventDate,
            IsArchived = false,
            CreatedAt = ev.CreatedAt,
            ChecklistItems = new()
        }, cancellation: ct);
    }
}
```

- [ ] **Step 3: Create EventDetailEndpoint**

Create `Quartermaster.Server/Events/EventDetailEndpoint.cs`:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventDetailRequest {
    public Guid Id { get; set; }
}

public class EventDetailEndpoint : Endpoint<EventDetailRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventDetailEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/events/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventDetailRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var chapter = _chapterRepo.Get(ev.ChapterId);
        var items = _eventRepo.GetChecklistItems(ev.Id);

        await SendAsync(new EventDetailDTO {
            Id = ev.Id,
            ChapterId = ev.ChapterId,
            ChapterName = chapter?.Name ?? "",
            InternalName = ev.InternalName,
            PublicName = ev.PublicName,
            Description = ev.Description,
            EventDate = ev.EventDate,
            IsArchived = ev.IsArchived,
            EventTemplateId = ev.EventTemplateId,
            CreatedAt = ev.CreatedAt,
            ChecklistItems = items.Select(i => new EventChecklistItemDTO {
                Id = i.Id,
                SortOrder = i.SortOrder,
                ItemType = (int)i.ItemType,
                Label = i.Label,
                IsCompleted = i.IsCompleted,
                CompletedAt = i.CompletedAt,
                Configuration = i.Configuration,
                ResultId = i.ResultId
            }).ToList()
        }, cancellation: ct);
    }
}
```

- [ ] **Step 4: Create EventUpdateEndpoint**

Create `Quartermaster.Server/Events/EventUpdateEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventUpdateEndpoint : Endpoint<EventUpdateRequest> {
    private readonly EventRepository _eventRepo;

    public EventUpdateEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Put("/api/events/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventUpdateRequest req, CancellationToken ct) {
        _eventRepo.Update(new Event {
            Id = req.Id,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate
        });

        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 5: Create EventArchiveEndpoint**

Create `Quartermaster.Server/Events/EventArchiveEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventArchiveRequest {
    public Guid Id { get; set; }
}

public class EventArchiveEndpoint : Endpoint<EventArchiveRequest> {
    private readonly EventRepository _eventRepo;

    public EventArchiveEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{Id}/archive");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventArchiveRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _eventRepo.SetArchived(req.Id, !ev.IsArchived);
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 6: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Server/Quartermaster.Server.csproj`

Expected: 0 errors.

---

## Task 7: Checklist Item Endpoints

**Files:**
- Create: `Quartermaster.Server/Events/ChecklistItemAddEndpoint.cs`
- Create: `Quartermaster.Server/Events/ChecklistItemUpdateEndpoint.cs`
- Create: `Quartermaster.Server/Events/ChecklistItemDeleteEndpoint.cs`
- Create: `Quartermaster.Server/Events/ChecklistItemCheckEndpoint.cs`
- Create: `Quartermaster.Server/Events/ChecklistItemUncheckEndpoint.cs`

- [ ] **Step 1: Create ChecklistItemAddEndpoint**

Create `Quartermaster.Server/Events/ChecklistItemAddEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemAddEndpoint : Endpoint<ChecklistItemCreateRequest, EventChecklistItemDTO> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemAddEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemCreateRequest req, CancellationToken ct) {
        var item = new EventChecklistItem {
            EventId = req.EventId,
            SortOrder = req.SortOrder,
            ItemType = (ChecklistItemType)req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration
        };

        _eventRepo.CreateChecklistItem(item);

        await SendAsync(new EventChecklistItemDTO {
            Id = item.Id,
            SortOrder = item.SortOrder,
            ItemType = req.ItemType,
            Label = item.Label,
            Configuration = item.Configuration
        }, cancellation: ct);
    }
}
```

- [ ] **Step 2: Create ChecklistItemUpdateEndpoint**

Create `Quartermaster.Server/Events/ChecklistItemUpdateEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemUpdateEndpoint : Endpoint<ChecklistItemUpdateRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemUpdateEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Put("/api/events/{EventId}/checklist/{ItemId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemUpdateRequest req, CancellationToken ct) {
        _eventRepo.UpdateChecklistItem(new EventChecklistItem {
            Id = req.ItemId,
            SortOrder = req.SortOrder,
            ItemType = (ChecklistItemType)req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration
        });

        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 3: Create ChecklistItemDeleteEndpoint**

Create `Quartermaster.Server/Events/ChecklistItemDeleteEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemDeleteRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
}

public class ChecklistItemDeleteEndpoint : Endpoint<ChecklistItemDeleteRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemDeleteEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Delete("/api/events/{EventId}/checklist/{ItemId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemDeleteRequest req, CancellationToken ct) {
        _eventRepo.DeleteChecklistItem(req.ItemId);
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 4: Create ChecklistItemCheckEndpoint**

Create `Quartermaster.Server/Events/ChecklistItemCheckEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemCheckEndpoint : Endpoint<ChecklistItemCheckRequest> {
    private readonly EventRepository _eventRepo;
    private readonly ChecklistItemExecutor _executor;

    public ChecklistItemCheckEndpoint(EventRepository eventRepo, ChecklistItemExecutor executor) {
        _eventRepo = eventRepo;
        _executor = executor;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/check");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemCheckRequest req, CancellationToken ct) {
        var item = _eventRepo.GetChecklistItem(req.ItemId);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (item.IsCompleted) {
            ThrowError("Item is already completed");
            return;
        }

        Guid? resultId = null;
        if (req.ExecuteAction && item.ItemType != ChecklistItemType.Text) {
            var (rid, error) = _executor.Execute(item);
            if (error != null) {
                ThrowError(error);
                return;
            }
            resultId = rid;
        }

        _eventRepo.CheckItem(item.Id, resultId);
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 5: Create ChecklistItemUncheckEndpoint**

Create `Quartermaster.Server/Events/ChecklistItemUncheckEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemUncheckRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
}

public class ChecklistItemUncheckEndpoint : Endpoint<ChecklistItemUncheckRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemUncheckEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/uncheck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemUncheckRequest req, CancellationToken ct) {
        var item = _eventRepo.GetChecklistItem(req.ItemId);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (item.ItemType != ChecklistItemType.Text) {
            ThrowError("Only text items can be unchecked");
            return;
        }

        _eventRepo.UncheckItem(item.Id);
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 6: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Server/Quartermaster.Server.csproj`

Expected: 0 errors.

---

## Task 8: Event Template Endpoints

**Files:**
- Create: `Quartermaster.Server/Events/EventTemplateListEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventTemplateCreateEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventTemplateDetailEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventTemplateDeleteEndpoint.cs`
- Create: `Quartermaster.Server/Events/EventFromTemplateEndpoint.cs`

- [ ] **Step 1: Create EventTemplateListEndpoint**

Create `Quartermaster.Server/Events/EventTemplateListEndpoint.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateListEndpoint : EndpointWithoutRequest<List<EventTemplateDTO>> {
    private readonly EventRepository _eventRepo;

    public EventTemplateListEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Get("/api/eventtemplates");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var templates = _eventRepo.GetAllTemplates();

        var dtos = templates.Select(t => {
            var variables = JsonSerializer.Deserialize<List<object>>(t.Variables) ?? new();
            var items = JsonSerializer.Deserialize<List<object>>(t.ChecklistItemTemplates) ?? new();
            return new EventTemplateDTO {
                Id = t.Id,
                Name = t.Name,
                VariableCount = variables.Count,
                ChecklistItemCount = items.Count,
                ChapterId = t.ChapterId,
                CreatedAt = t.CreatedAt
            };
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
```

- [ ] **Step 2: Create EventTemplateCreateEndpoint**

Create `Quartermaster.Server/Events/EventTemplateCreateEndpoint.cs`:

```csharp
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateCreateEndpoint : Endpoint<EventTemplateCreateRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;

    public EventTemplateCreateEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/eventtemplates");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventTemplateCreateRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.EventId);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var items = _eventRepo.GetChecklistItems(ev.Id);

        var checklistTemplates = items.Select(i => new {
            sortOrder = i.SortOrder,
            itemType = (int)i.ItemType,
            label = i.Label,
            configuration = i.Configuration
        }).ToList();

        var template = new EventTemplate {
            Name = req.Name,
            PublicNameTemplate = ev.PublicName,
            DescriptionTemplate = ev.Description,
            Variables = req.Variables,
            ChecklistItemTemplates = JsonSerializer.Serialize(checklistTemplates),
            ChapterId = ev.ChapterId,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.CreateTemplate(template);

        await SendAsync(new EventTemplateDetailDTO {
            Id = template.Id,
            Name = template.Name,
            PublicNameTemplate = template.PublicNameTemplate,
            DescriptionTemplate = template.DescriptionTemplate,
            Variables = template.Variables,
            ChecklistItemTemplates = template.ChecklistItemTemplates,
            ChapterId = template.ChapterId,
            CreatedAt = template.CreatedAt
        }, cancellation: ct);
    }
}
```

- [ ] **Step 3: Create EventTemplateDetailEndpoint**

Create `Quartermaster.Server/Events/EventTemplateDetailEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateDetailRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDetailEndpoint : Endpoint<EventTemplateDetailRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;

    public EventTemplateDetailEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Get("/api/eventtemplates/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventTemplateDetailRequest req, CancellationToken ct) {
        var template = _eventRepo.GetTemplate(req.Id);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new EventTemplateDetailDTO {
            Id = template.Id,
            Name = template.Name,
            PublicNameTemplate = template.PublicNameTemplate,
            DescriptionTemplate = template.DescriptionTemplate,
            Variables = template.Variables,
            ChecklistItemTemplates = template.ChecklistItemTemplates,
            ChapterId = template.ChapterId,
            CreatedAt = template.CreatedAt
        }, cancellation: ct);
    }
}
```

- [ ] **Step 4: Create EventTemplateDeleteEndpoint**

Create `Quartermaster.Server/Events/EventTemplateDeleteEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateDeleteRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDeleteEndpoint : Endpoint<EventTemplateDeleteRequest> {
    private readonly EventRepository _eventRepo;

    public EventTemplateDeleteEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Delete("/api/eventtemplates/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventTemplateDeleteRequest req, CancellationToken ct) {
        _eventRepo.DeleteTemplate(req.Id);
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 5: Create EventFromTemplateEndpoint**

Create `Quartermaster.Server/Events/EventFromTemplateEndpoint.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventFromTemplateEndpoint : Endpoint<EventFromTemplateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventFromTemplateEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/events/from-template");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventFromTemplateRequest req, CancellationToken ct) {
        var template = _eventRepo.GetTemplate(req.TemplateId);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        // Replace variables in all text fields
        var publicName = ReplaceVariables(template.PublicNameTemplate, req.VariableValues);
        var description = template.DescriptionTemplate != null
            ? ReplaceVariables(template.DescriptionTemplate, req.VariableValues)
            : null;

        var ev = new Event {
            ChapterId = req.ChapterId,
            InternalName = publicName,
            PublicName = publicName,
            Description = description,
            EventTemplateId = template.Id,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.Create(ev);

        // Create checklist items from template
        var itemTemplates = JsonSerializer.Deserialize<List<ChecklistItemTemplate>>(
            template.ChecklistItemTemplates) ?? new();

        var createdItems = new List<EventChecklistItemDTO>();
        foreach (var itemTemplate in itemTemplates) {
            var label = ReplaceVariables(itemTemplate.Label, req.VariableValues);
            var config = itemTemplate.Configuration != null
                ? ReplaceVariables(itemTemplate.Configuration, req.VariableValues)
                : null;

            var item = new EventChecklistItem {
                EventId = ev.Id,
                SortOrder = itemTemplate.SortOrder,
                ItemType = (ChecklistItemType)itemTemplate.ItemType,
                Label = label,
                Configuration = config
            };
            _eventRepo.CreateChecklistItem(item);

            createdItems.Add(new EventChecklistItemDTO {
                Id = item.Id,
                SortOrder = item.SortOrder,
                ItemType = itemTemplate.ItemType,
                Label = item.Label,
                Configuration = item.Configuration
            });
        }

        var chapter = _chapterRepo.Get(ev.ChapterId);

        await SendAsync(new EventDetailDTO {
            Id = ev.Id,
            ChapterId = ev.ChapterId,
            ChapterName = chapter?.Name ?? "",
            InternalName = ev.InternalName,
            PublicName = ev.PublicName,
            Description = ev.Description,
            EventDate = ev.EventDate,
            IsArchived = false,
            EventTemplateId = ev.EventTemplateId,
            CreatedAt = ev.CreatedAt,
            ChecklistItems = createdItems
        }, cancellation: ct);
    }

    private static string ReplaceVariables(string text, Dictionary<string, string> values) {
        foreach (var (name, value) in values) {
            text = text.Replace($"{{{{{name}}}}}", value);
        }
        return text;
    }

    private class ChecklistItemTemplate {
        public int SortOrder { get; set; }
        public int ItemType { get; set; }
        public string Label { get; set; } = "";
        public string? Configuration { get; set; }
    }
}
```

- [ ] **Step 6: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Server/Quartermaster.Server.csproj`

Expected: 0 errors.

---

## Task 9: Blazor Event List + Create Pages

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/EventList.razor` + `.cs`
- Create: `Quartermaster.Blazor/Pages/Administration/EventCreate.razor` + `.cs`
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor`

- [ ] **Step 1: Create EventList page**

Create `Quartermaster.Blazor/Pages/Administration/EventList.razor` — paginated table with chapter filter, archive toggle, header buttons for "Neues Event" and "Aus Vorlage erstellen" (links to template list). Columns: Public Name (link to detail), Chapter, Date, Progress (e.g. "3/5"), Created. Follow the `MemberList.razor` pattern exactly.

Create `Quartermaster.Blazor/Pages/Administration/EventList.razor.cs` — code-behind with chapter filter, archive toggle, pagination. Same pattern as `MemberList.razor.cs`.

- [ ] **Step 2: Create EventCreate page**

Create `Quartermaster.Blazor/Pages/Administration/EventCreate.razor` — form with chapter picker, internal name, public name, description textarea (markdown), date picker. On submit creates event and navigates to detail page. Follow the `MotionCreate.razor` pattern.

Create `Quartermaster.Blazor/Pages/Administration/EventCreate.razor.cs` — code-behind with form fields, chapter list loading, submit handler that POSTs to `/api/events` and navigates to `/Administration/Events/{id}`.

- [ ] **Step 3: Update MainLayout navigation**

In `Quartermaster.Blazor/Layout/MainLayout.razor`, find the "Vorstandsarbeit" dropdown. Add after the "Mitglieder" link:

```razor
<li><hr class="dropdown-divider"></li>
<li><a class="dropdown-item" href="/Administration/Events">Events</a></li>
<li><a class="dropdown-item" href="/Administration/EventTemplates">Event-Vorlagen</a></li>
```

- [ ] **Step 4: Build and verify**

Run: `export DOTNET_ROOT=/usr/lib/dotnet && export PATH="$DOTNET_ROOT:$PATH" && dotnet build Quartermaster.Blazor/Quartermaster.Blazor.csproj`

Expected: 0 errors.

---

## Task 10: Blazor Event Detail Page

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/EventDetail.razor` + `.cs`

This is the most complex page. It shows event details, the checklist with typed items, and action buttons.

- [ ] **Step 1: Create EventDetail.razor**

The page should include:
- Back button to event list
- Header: public name + chapter + date + progress badge
- **Details card:** Internal name, rendered markdown description, event date
- **Checklist card:** Ordered list of items where:
  - Text items (uncompleted): checkbox + label, clicking checks it
  - Text items (completed): checked checkbox + label, clicking unchecks it
  - Action items (uncompleted): label + type badge + two buttons ("Erstellen & abhaken"/"Senden & abhaken" and "Bereits erledigt")
  - Action items (completed): checked + label + link to result if ResultId set
  - SendEmail items: collapsible preview using client-side `TemplateRenderer` with mock `MemberDetailDTO`
- **Add checklist item form:** Type dropdown (Text/CreateMotion/SendEmail), label input, type-specific config fields:
  - Text: just label
  - CreateMotion: chapter picker, motion title, motion text (textarea)
  - SendEmail: target type dropdown (Chapter/AdministrativeDivision), target picker, option template identifier input
- **Footer buttons:** "Bearbeiten" (inline or navigate), "Als Vorlage speichern" (navigates to save template page), "Archivieren"/"Dearchivieren" toggle

Route: `/Administration/Events/{Id:guid}`

- [ ] **Step 2: Create EventDetail.razor.cs**

Code-behind with:
- Load event detail on init
- `CheckItem(Guid itemId, bool executeAction)` — POSTs to check endpoint, reloads
- `UncheckItem(Guid itemId)` — POSTs to uncheck endpoint, reloads
- `AddChecklistItem()` — POSTs new item, reloads
- `DeleteChecklistItem(Guid itemId)` — DELETEs item, reloads
- `ToggleArchive()` — POSTs to archive endpoint, reloads
- `ToggleEmailPreview(Guid itemId)` — toggles preview for a specific email item
- `RenderEmailPreview(string templateIdentifier)` — uses shared `TemplateRenderer` + `TemplateMockDataProvider` client-side to render preview HTML
- Helper methods: `RoleLabel()`, item type labels, confirmation handling

- [ ] **Step 3: Build and verify**

---

## Task 11: Blazor Event Template Pages

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/EventTemplateList.razor` + `.cs`
- Create: `Quartermaster.Blazor/Pages/Administration/EventTemplateSave.razor` + `.cs`
- Create: `Quartermaster.Blazor/Pages/Administration/EventCreateFromTemplate.razor` + `.cs`

- [ ] **Step 1: Create EventTemplateList page**

Simple table: Name, Variable count, Checklist item count, Created, Delete button, "Aus Vorlage erstellen" button per row linking to `/Administration/Events/CreateFromTemplate/{id}`.

Route: `/Administration/EventTemplates`

- [ ] **Step 2: Create EventTemplateSave page**

Route: `/Administration/Events/{EventId:guid}/SaveAsTemplate`

Page loads the event, scans PublicName + Description + all checklist item Labels + Configurations for `{{...}}` patterns using a regex. Displays:
- Template name input (pre-filled from event InternalName)
- List of detected variables, each with Label input + Type dropdown (Text/Date/Time/Number/OptionTemplate/Chapter)
- Save button POSTs to `/api/eventtemplates`

Code-behind: regex `\{\{(\w+)\}\}` to find variables, deduplicates, POSTs create request with event ID + name + variables JSON.

- [ ] **Step 3: Create EventCreateFromTemplate page**

Route: `/Administration/Events/CreateFromTemplate/{TemplateId:guid}`

Page loads the template detail, parses Variables JSON, renders typed inputs:
- Text → `<input type="text">`
- Date → `<input type="date">`
- Time → `<input type="time">`
- Number → `<input type="number">`
- OptionTemplate → text input (option template identifier) — can be enhanced with search later
- Chapter → chapter picker dropdown

Also shows chapter picker for the event and live preview of resolved public name.

On submit: POSTs to `/api/events/from-template` with template ID, chapter ID, and variable values dictionary. Navigates to the created event's detail page.

- [ ] **Step 4: Build and verify**

---

## Task 12: End-to-End Test

- [ ] **Step 1: Start server and verify all event endpoints respond**

- [ ] **Step 2: Create an event manually via the UI**

Navigate to `/Administration/Events/Create`, fill in a chapter + names + description + date. Verify redirect to detail page.

- [ ] **Step 3: Add checklist items**

On the event detail page, add:
- A text item
- A CreateMotion item (with chapter, title, text)
- A SendEmail item (with chapter target, template identifier)

- [ ] **Step 4: Test checklist execution**

- Check the text item, verify it toggles
- Uncheck the text item, verify it toggles back
- Click "Erstellen & abhaken" on the motion item, verify motion is created and link appears
- Click "Bereits erledigt" on the email item, verify it's checked without sending

- [ ] **Step 5: Test email preview**

Expand the email preview on an unchecked email item. Verify it renders the template with mock member data.

- [ ] **Step 6: Test save as template**

Click "Als Vorlage speichern", verify variables are detected, set types, save.

- [ ] **Step 7: Test create from template**

Navigate to template list, click create from template, fill in variables, verify event is created with resolved text.
