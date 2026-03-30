# Options & Template System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a configurable options system with global defaults and per-chapter overrides (walking up the chapter hierarchy), with support for Fluid/Liquid template options rendered from markdown. Includes an admin page for editing options and a live template preview.

**Architecture:** New `SystemOption` entity stores key-value pairs with DataType (Number/String/Template) and optional ChapterId. `OptionDefinition` entity defines which options exist, their friendly names, data types, whether they're overridable per chapter, and which template models they need for preview. Resolution walks up the chapter hierarchy: most-specific chapter override wins, falling back to global default. Chapter gets a `ShortCode` field for prefix resolution. Template options use Fluid (Liquid) for variable substitution and Markdig for markdown→HTML. Preview endpoint accepts template text + mock data and returns rendered HTML.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB, Blazor WASM, Fluid.Core 3.0.0-beta.5, Markdig, Bootstrap 5

---

## File Structure

### Data Model
- `Quartermaster.Data/Chapters/Chapter.cs` — add `ShortCode` field
- `Quartermaster.Data/Options/SystemOption.cs` — option value entity
- `Quartermaster.Data/Options/OptionDefinition.cs` — option metadata (what options exist)
- `Quartermaster.Data/Options/OptionRepository.cs` — CRUD + hierarchy resolution
- `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs` — add tables + Chapter.ShortCode
- `Quartermaster.Data/DbContext.cs` — register tables/repos
- `Quartermaster.Data/Chapters/ChapterRepository.cs` — update seeding with ShortCodes, add GetAncestorChapters

### API DTOs
- `Quartermaster.Api/Options/OptionDefinitionDTO.cs`
- `Quartermaster.Api/Options/SystemOptionDTO.cs`
- `Quartermaster.Api/Options/OptionUpdateRequest.cs`
- `Quartermaster.Api/Options/TemplatePreviewRequest.cs`
- `Quartermaster.Api/Options/TemplatePreviewResponse.cs`

### Server
- `Quartermaster.Server/Options/OptionListEndpoint.cs` — GET list all definitions with resolved values
- `Quartermaster.Server/Options/OptionUpdateEndpoint.cs` — POST update value
- `Quartermaster.Server/Options/TemplatePreviewEndpoint.cs` — POST render template with mock data
- `Quartermaster.Server/Options/TemplateRenderer.cs` — Fluid + Markdig rendering logic
- `Quartermaster.Server/Options/TemplateMockDataProvider.cs` — generates mock data per template model

### Blazor
- `Quartermaster.Blazor/Pages/Administration/OptionList.razor` + `.cs` — view/edit options
- `Quartermaster.Blazor/Layout/MainLayout.razor` — add nav link

---

## Task 1: Chapter ShortCode + Option Entities + Migration

**Files:**
- Modify: `Quartermaster.Data/Chapters/Chapter.cs`
- Create: `Quartermaster.Data/Options/SystemOption.cs`
- Create: `Quartermaster.Data/Options/OptionDefinition.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`
- Modify: `Quartermaster.Data/DbContext.cs`

- [ ] **Step 1: Add ShortCode to Chapter**

Add to `Chapter.cs`:
```csharp
    public string? ShortCode { get; set; }
```

- [ ] **Step 2: Create SystemOption entity**

Create `Quartermaster.Data/Options/SystemOption.cs`:
```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Options;

[Table(TableName, IsColumnAttributeRequired = false)]
public class SystemOption {
    public const string TableName = "SystemOptions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Identifier { get; set; } = "";
    public string Value { get; set; } = "";
    public Guid? ChapterId { get; set; }
}
```

- [ ] **Step 3: Create OptionDefinition entity**

Create `Quartermaster.Data/Options/OptionDefinition.cs`:
```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Options;

[Table(TableName, IsColumnAttributeRequired = false)]
public class OptionDefinition {
    public const string TableName = "OptionDefinitions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Identifier { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public OptionDataType DataType { get; set; }
    public bool IsOverridable { get; set; }

    /// <summary>
    /// Comma-separated list of model names available for template preview.
    /// E.g. "MembershipApplication,Chapter"
    /// </summary>
    public string TemplateModels { get; set; } = "";
}

public enum OptionDataType {
    String,
    Number,
    Template
}
```

- [ ] **Step 4: Update M001 migration**

Add `using Quartermaster.Data.Options;` to the migration.

Add `ShortCode` to the Chapters table:
```csharp
            .WithColumn(nameof(Chapter.ShortCode)).AsString(32).Nullable();
```

Add after the MotionVotes table (before DueSelections):
```csharp
        Create.Table(OptionDefinition.TableName)
            .WithColumn(nameof(OptionDefinition.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(OptionDefinition.Identifier)).AsString(256).Unique()
            .WithColumn(nameof(OptionDefinition.FriendlyName)).AsString(256)
            .WithColumn(nameof(OptionDefinition.DataType)).AsInt32()
            .WithColumn(nameof(OptionDefinition.IsOverridable)).AsBoolean()
            .WithColumn(nameof(OptionDefinition.TemplateModels)).AsString(512);

        Create.Table(SystemOption.TableName)
            .WithColumn(nameof(SystemOption.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SystemOption.Identifier)).AsString(256)
            .WithColumn(nameof(SystemOption.Value)).AsString(8192)
            .WithColumn(nameof(SystemOption.ChapterId)).AsGuid().Nullable();

        Create.ForeignKey("FK_SystemOptions_ChapterId_Chapters_Id")
            .FromTable(SystemOption.TableName).ForeignColumn(nameof(SystemOption.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));
```

Add to Down():
```csharp
        Delete.ForeignKey("FK_SystemOptions_ChapterId_Chapters_Id")
            .OnTable(SystemOption.TableName);
        Delete.Table(SystemOption.TableName);
        Delete.Table(OptionDefinition.TableName);
```

- [ ] **Step 5: Register in DbContext**

Add:
- `using Quartermaster.Data.Options;`
- `public ITable<SystemOption> SystemOptions => this.GetTable<SystemOption>();`
- `public ITable<OptionDefinition> OptionDefinitions => this.GetTable<OptionDefinition>();`
- `services.AddScoped<OptionRepository>();` in AddRepositories

- [ ] **Step 6: Build and verify**

Run: `dotnet build`

---

## Task 2: Chapter ShortCode Seeding + OptionRepository

**Files:**
- Modify: `Quartermaster.Data/Chapters/ChapterRepository.cs`
- Create: `Quartermaster.Data/Options/OptionRepository.cs`

- [ ] **Step 1: Update Chapter seeding with ShortCodes**

In `ChapterRepository.SupplementDefaults`, update the Bundesverband creation:
```csharp
        var bundesverband = new Chapter {
            Id = Guid.NewGuid(),
            Name = "Piratenpartei Deutschland",
            AdministrativeDivisionId = deDivision.Id,
            ParentChapterId = null,
            ShortCode = "de"
        };
```

For state chapters, add a ShortCode mapping. Add this dictionary before the loop:
```csharp
        var shortCodes = new Dictionary<string, string> {
            ["Baden-Württemberg"] = "bw", ["Bayern"] = "by", ["Berlin"] = "be",
            ["Brandenburg"] = "bb", ["Bremen"] = "hb", ["Hamburg"] = "hh",
            ["Hessen"] = "he", ["Mecklenburg-Vorpommern"] = "mv",
            ["Niedersachsen"] = "nds", ["Nordrhein-Westfalen"] = "nrw",
            ["Rheinland-Pfalz"] = "rlp", ["Saarland"] = "sl", ["Sachsen"] = "sn",
            ["Sachsen-Anhalt"] = "st", ["Schleswig-Holstein"] = "sh", ["Thüringen"] = "th"
        };
```

Update the state chapter creation:
```csharp
            Create(new Chapter {
                Id = Guid.NewGuid(),
                Name = $"Piratenpartei {state.Name}",
                AdministrativeDivisionId = state.Id,
                ParentChapterId = bundesverband.Id,
                ShortCode = shortCodes.GetValueOrDefault(state.Name)
            });
```

- [ ] **Step 2: Add GetAncestorChapters to ChapterRepository**

```csharp
    public List<Chapter> GetAncestorChain(Guid chapterId) {
        var chain = new List<Chapter>();
        var current = Get(chapterId);
        while (current != null) {
            chain.Add(current);
            if (current.ParentChapterId == null || current.ParentChapterId == current.Id)
                break;
            current = Get(current.ParentChapterId.Value);
        }
        return chain;
    }
```

- [ ] **Step 3: Create OptionRepository**

Create `Quartermaster.Data/Options/OptionRepository.cs`:
```csharp
using LinqToDB;
using Quartermaster.Data.Chapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Options;

public class OptionRepository {
    private readonly DbContext _context;

    public OptionRepository(DbContext context) {
        _context = context;
    }

    public List<OptionDefinition> GetAllDefinitions()
        => _context.OptionDefinitions.OrderBy(d => d.Identifier).ToList();

    public OptionDefinition? GetDefinition(string identifier)
        => _context.OptionDefinitions.Where(d => d.Identifier == identifier).FirstOrDefault();

    public void CreateDefinition(OptionDefinition def) => _context.Insert(def);

    public SystemOption? GetGlobalValue(string identifier)
        => _context.SystemOptions
            .Where(o => o.Identifier == identifier && o.ChapterId == null)
            .FirstOrDefault();

    public SystemOption? GetChapterValue(string identifier, Guid chapterId)
        => _context.SystemOptions
            .Where(o => o.Identifier == identifier && o.ChapterId == chapterId)
            .FirstOrDefault();

    public List<SystemOption> GetAllValues()
        => _context.SystemOptions.ToList();

    public List<SystemOption> GetValuesForIdentifier(string identifier)
        => _context.SystemOptions.Where(o => o.Identifier == identifier).ToList();

    /// <summary>
    /// Resolves the effective value for an option, walking up the chapter hierarchy.
    /// Returns the most specific override, or the global default.
    /// </summary>
    public string? ResolveValue(string identifier, Guid? chapterId, ChapterRepository chapterRepo) {
        if (chapterId.HasValue) {
            var chain = chapterRepo.GetAncestorChain(chapterId.Value);
            foreach (var chapter in chain) {
                var chapterValue = GetChapterValue(identifier, chapter.Id);
                if (chapterValue != null)
                    return chapterValue.Value;
            }
        }

        var global = GetGlobalValue(identifier);
        return global?.Value;
    }

    public void SetValue(string identifier, Guid? chapterId, string value) {
        var existing = chapterId.HasValue
            ? GetChapterValue(identifier, chapterId.Value)
            : GetGlobalValue(identifier);

        if (existing != null) {
            _context.SystemOptions
                .Where(o => o.Id == existing.Id)
                .Set(o => o.Value, value)
                .Update();
        } else {
            _context.Insert(new SystemOption {
                Identifier = identifier,
                Value = value,
                ChapterId = chapterId
            });
        }
    }

    public void SupplementDefaults() {
        AddDefinitionIfNotExists("templates.membershipapplication.approved.email",
            "E-Mail: Mitgliedsantrag genehmigt",
            OptionDataType.Template, true,
            "MembershipApplicationDetailDTO,ChapterDTO",
            "Hallo **{{ application.FirstName }}**,\n\ndein Mitgliedsantrag bei der **{{ chapter.Name }}** wurde genehmigt.\n\nWillkommen an Bord!\n");

        AddDefinitionIfNotExists("templates.membershipapplication.rejected.email",
            "E-Mail: Mitgliedsantrag abgelehnt",
            OptionDataType.Template, true,
            "MembershipApplicationDetailDTO,ChapterDTO",
            "Hallo **{{ application.FirstName }}**,\n\nleider wurde dein Mitgliedsantrag bei der **{{ chapter.Name }}** abgelehnt.\n");

        AddDefinitionIfNotExists("templates.dueselection.approved.email",
            "E-Mail: Beitragsminderung genehmigt",
            OptionDataType.Template, true,
            "DueSelectionDetailDTO,ChapterDTO",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde genehmigt.\n");

        AddDefinitionIfNotExists("templates.dueselection.rejected.email",
            "E-Mail: Beitragsminderung abgelehnt",
            OptionDataType.Template, true,
            "DueSelectionDetailDTO,ChapterDTO",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde leider abgelehnt.\n");

        AddDefinitionIfNotExists("general.chaptername.display",
            "Anzeigename der Gliederung",
            OptionDataType.String, true, "", "");

        AddDefinitionIfNotExists("general.contact.email",
            "Kontakt E-Mail Adresse",
            OptionDataType.String, true, "", "");
    }

    private void AddDefinitionIfNotExists(string identifier, string friendlyName,
        OptionDataType dataType, bool isOverridable, string templateModels, string defaultValue) {

        if (GetDefinition(identifier) != null)
            return;

        CreateDefinition(new OptionDefinition {
            Identifier = identifier,
            FriendlyName = friendlyName,
            DataType = dataType,
            IsOverridable = isOverridable,
            TemplateModels = templateModels
        });

        if (!string.IsNullOrEmpty(defaultValue))
            SetValue(identifier, null, defaultValue);
    }
}
```

- [ ] **Step 4: Register SupplementDefaults**

In `DbContext.SupplementDefaults`, add after permissions:
```csharp
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SupplementDefaults();
```

- [ ] **Step 5: Build and verify**

---

## Task 3: API DTOs

**Files:**
- Create: `Quartermaster.Api/Options/OptionDefinitionDTO.cs`
- Create: `Quartermaster.Api/Options/SystemOptionDTO.cs`
- Create: `Quartermaster.Api/Options/OptionUpdateRequest.cs`
- Create: `Quartermaster.Api/Options/TemplatePreviewRequest.cs`
- Create: `Quartermaster.Api/Options/TemplatePreviewResponse.cs`

- [ ] **Step 1: Create all DTOs**

`OptionDefinitionDTO.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Options;

public class OptionDefinitionDTO {
    public string Identifier { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public int DataType { get; set; }
    public bool IsOverridable { get; set; }
    public string TemplateModels { get; set; } = "";
    public string GlobalValue { get; set; } = "";
    public List<OptionOverrideDTO> Overrides { get; set; } = [];
}

public class OptionOverrideDTO {
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string ChapterShortCode { get; set; } = "";
    public string Value { get; set; } = "";
}
```

`SystemOptionDTO.cs`:
```csharp
using System;

namespace Quartermaster.Api.Options;

public class SystemOptionDTO {
    public string Identifier { get; set; } = "";
    public string Value { get; set; } = "";
    public Guid? ChapterId { get; set; }
}
```

`OptionUpdateRequest.cs`:
```csharp
using System;

namespace Quartermaster.Api.Options;

public class OptionUpdateRequest {
    public string Identifier { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string Value { get; set; } = "";
}
```

`TemplatePreviewRequest.cs`:
```csharp
namespace Quartermaster.Api.Options;

public class TemplatePreviewRequest {
    public string TemplateText { get; set; } = "";
    public string TemplateModels { get; set; } = "";
}
```

`TemplatePreviewResponse.cs`:
```csharp
namespace Quartermaster.Api.Options;

public class TemplatePreviewResponse {
    public string RenderedHtml { get; set; } = "";
    public string? Error { get; set; }
}
```

- [ ] **Step 2: Build and verify**

---

## Task 4: Template Renderer + Mock Data Provider

**Files:**
- Create: `Quartermaster.Server/Options/TemplateRenderer.cs`
- Create: `Quartermaster.Server/Options/TemplateMockDataProvider.cs`

- [ ] **Step 1: Create TemplateRenderer**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fluid;
using Markdig;

namespace Quartermaster.Server.Options;

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

- [ ] **Step 2: Create TemplateMockDataProvider**

This generates sample data for each known model type, using the API DTOs so they work on both server and client.

```csharp
using System;
using System.Collections.Generic;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.Options;

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
            }
        }

        return data;
    }
}
```

- [ ] **Step 3: Build and verify**

---

## Task 5: Server Endpoints

**Files:**
- Create: `Quartermaster.Server/Options/OptionListEndpoint.cs`
- Create: `Quartermaster.Server/Options/OptionUpdateEndpoint.cs`
- Create: `Quartermaster.Server/Options/TemplatePreviewEndpoint.cs`

- [ ] **Step 1: Create OptionListEndpoint**

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FastEndpoints;
using Quartermaster.Api.Options;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Options;

public class OptionListEndpoint : EndpointWithoutRequest<List<OptionDefinitionDTO>> {
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;

    public OptionListEndpoint(OptionRepository optionRepo, ChapterRepository chapterRepo) {
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/options");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var definitions = _optionRepo.GetAllDefinitions();
        var allValues = _optionRepo.GetAllValues();
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id);

        var dtos = definitions.Select(def => {
            var values = allValues.Where(v => v.Identifier == def.Identifier).ToList();
            var globalValue = values.FirstOrDefault(v => v.ChapterId == null)?.Value ?? "";
            var overrides = values
                .Where(v => v.ChapterId != null && chapters.ContainsKey(v.ChapterId.Value))
                .Select(v => {
                    var ch = chapters[v.ChapterId!.Value];
                    return new OptionOverrideDTO {
                        ChapterId = ch.Id,
                        ChapterName = ch.Name,
                        ChapterShortCode = ch.ShortCode ?? "",
                        Value = v.Value
                    };
                }).ToList();

            return new OptionDefinitionDTO {
                Identifier = def.Identifier,
                FriendlyName = def.FriendlyName,
                DataType = (int)def.DataType,
                IsOverridable = def.IsOverridable,
                TemplateModels = def.TemplateModels,
                GlobalValue = globalValue,
                Overrides = overrides
            };
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
```

- [ ] **Step 2: Create OptionUpdateEndpoint**

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Options;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Options;

public class OptionUpdateEndpoint : Endpoint<OptionUpdateRequest> {
    private readonly OptionRepository _optionRepo;

    public OptionUpdateEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Post("/api/options");
        AllowAnonymous();
    }

    public override async Task HandleAsync(OptionUpdateRequest req, CancellationToken ct) {
        var def = _optionRepo.GetDefinition(req.Identifier);
        if (def == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (req.ChapterId.HasValue && !def.IsOverridable) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _optionRepo.SetValue(req.Identifier, req.ChapterId, req.Value);
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 3: Create TemplatePreviewEndpoint**

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

public class TemplatePreviewEndpoint : Endpoint<TemplatePreviewRequest, TemplatePreviewResponse> {
    public override void Configure() {
        Post("/api/options/preview");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TemplatePreviewRequest req, CancellationToken ct) {
        var mockData = TemplateMockDataProvider.GetMockData(req.TemplateModels);
        var (html, error) = await TemplateRenderer.RenderAsync(req.TemplateText, mockData);

        await SendAsync(new TemplatePreviewResponse {
            RenderedHtml = html ?? "",
            Error = error
        }, cancellation: ct);
    }
}
```

- [ ] **Step 4: Build and verify**

---

## Task 6: Blazor Options Admin Page

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/OptionList.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/OptionList.razor.cs`

- [ ] **Step 1: Create OptionList.razor**

Page showing all option definitions with their global values and chapter overrides. Each option is an expandable card. Template options have an edit textarea with live preview button. String/Number options have a simple input. Overridable options show a chapter selector to add/edit overrides.

```razor
@page "/Administration/Options"
@using Quartermaster.Api.Options
@using Quartermaster.Api.Chapters

<div class="mb-3">
    <h3>Verwaltung - Einstellungen</h3>
</div>

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Options != null) {
    @foreach (var opt in Options) {
        <div class="card mb-3">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <h5 class="mb-0">@opt.FriendlyName</h5>
                        <code class="text-secondary">@opt.Identifier</code>
                    </div>
                    <span class="badge border @DataTypeBadge(opt.DataType)">@DataTypeLabel(opt.DataType)</span>
                </div>

                <hr />

                <h6>Globaler Wert</h6>
                @if (opt.DataType == 2) {
                    <textarea class="form-control font-monospace mb-2" rows="6"
                              @bind="opt.GlobalValue" @bind:event="oninput"></textarea>
                    <div class="d-flex gap-2 mb-2">
                        <button class="btn btn-sm btn-outline-primary" @onclick="() => SaveGlobal(opt)">
                            <i class="bi bi-save"></i> Speichern
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" @onclick="() => Preview(opt)">
                            <i class="bi bi-eye"></i> Vorschau
                        </button>
                    </div>
                    @if (PreviewIdentifier == opt.Identifier && PreviewHtml != null) {
                        <div class="card bg-body-secondary">
                            <div class="card-body">
                                @((MarkupString)PreviewHtml)
                            </div>
                        </div>
                    }
                } else {
                    <div class="input-group mb-2" style="max-width: 500px;">
                        <input type="@(opt.DataType == 1 ? "number" : "text")" class="form-control"
                               @bind="opt.GlobalValue" @bind:event="oninput" />
                        <button class="btn btn-outline-primary" @onclick="() => SaveGlobal(opt)">
                            <i class="bi bi-save"></i> Speichern
                        </button>
                    </div>
                }

                @if (opt.IsOverridable) {
                    <hr />
                    <h6>Gliederungs-Überschreibungen</h6>

                    @if (opt.Overrides.Count > 0) {
                        @foreach (var ov in opt.Overrides) {
                            <div class="d-flex gap-2 align-items-center mb-2">
                                <span class="badge bg-secondary">@ov.ChapterShortCode</span>
                                <span>@ov.ChapterName</span>
                                @if (opt.DataType == 2) {
                                    <button class="btn btn-sm btn-outline-secondary" @onclick="() => EditOverride(opt, ov)">
                                        <i class="bi bi-pencil"></i>
                                    </button>
                                } else {
                                    <input type="@(opt.DataType == 1 ? "number" : "text")" class="form-control form-control-sm"
                                           style="max-width: 300px;" value="@ov.Value"
                                           @onchange="(e) => SaveOverride(opt.Identifier, ov.ChapterId, e.Value?.ToString() ?? "")" />
                                }
                            </div>
                        }
                    }

                    <div class="d-flex gap-2 align-items-center mt-2">
                        <select class="form-select form-select-sm" style="width: auto;" @bind="NewOverrideChapterId">
                            <option value="">Gliederung hinzufügen...</option>
                            @if (Chapters != null) {
                                @foreach (var ch in Chapters.Where(c => !string.IsNullOrEmpty(c.ShortCode))) {
                                    @if (!opt.Overrides.Any(o => o.ChapterId == ch.Id)) {
                                        <option value="@ch.Id">@ch.Name (@ch.ShortCode)</option>
                                    }
                                }
                            }
                        </select>
                        <button class="btn btn-sm btn-outline-success" @onclick="() => AddOverride(opt)">
                            <i class="bi bi-plus"></i> Hinzufügen
                        </button>
                    </div>
                }
            </div>
        </div>
    }
}
```

- [ ] **Step 2: Create OptionList.razor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OptionList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<OptionDefinitionDTO>? Options;
    private List<ChapterDTO>? Chapters;
    private bool Loading = true;
    private string NewOverrideChapterId { get; set; } = "";
    private string? PreviewHtml;
    private string? PreviewIdentifier;

    protected override async Task OnInitializedAsync() {
        Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
        await LoadOptions();
    }

    private async Task LoadOptions() {
        Loading = true;
        Options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
        Loading = false;
    }

    private async Task SaveGlobal(OptionDefinitionDTO opt) {
        await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = opt.Identifier,
            ChapterId = null,
            Value = opt.GlobalValue
        });
        ToastService.Toast("Gespeichert.", "success");
    }

    private async Task SaveOverride(string identifier, Guid chapterId, string value) {
        await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = identifier,
            ChapterId = chapterId,
            Value = value
        });
        ToastService.Toast("Gespeichert.", "success");
    }

    private async Task AddOverride(OptionDefinitionDTO opt) {
        if (!Guid.TryParse(NewOverrideChapterId, out var chapterId))
            return;

        await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = opt.Identifier,
            ChapterId = chapterId,
            Value = opt.GlobalValue
        });

        NewOverrideChapterId = "";
        await LoadOptions();
        StateHasChanged();
    }

    private void EditOverride(OptionDefinitionDTO opt, OptionOverrideDTO ov) {
        // For template overrides, just copy to global for editing
        // A proper modal editor would be better but this works for now
        opt.GlobalValue = ov.Value;
        StateHasChanged();
    }

    private async Task Preview(OptionDefinitionDTO opt) {
        var result = await Http.PostAsJsonAsync("/api/options/preview", new TemplatePreviewRequest {
            TemplateText = opt.GlobalValue,
            TemplateModels = opt.TemplateModels
        });

        var response = await result.Content.ReadFromJsonAsync<TemplatePreviewResponse>();
        PreviewIdentifier = opt.Identifier;
        if (response?.Error != null)
            PreviewHtml = $"<p class=\"text-danger\">{response.Error}</p>";
        else
            PreviewHtml = response?.RenderedHtml ?? "";
        StateHasChanged();
    }

    private static string DataTypeLabel(int dt) => dt switch {
        0 => "Text",
        1 => "Zahl",
        2 => "Template",
        _ => "?"
    };

    private static string DataTypeBadge(int dt) => dt switch {
        0 => "border-info text-info-emphasis",
        1 => "border-primary text-primary-emphasis",
        2 => "border-warning text-warning-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
```

- [ ] **Step 3: Build and verify**

---

## Task 7: Navigation

**Files:**
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor`

- [ ] **Step 1: Add options link**

In the Verwaltung dropdown, add:
```razor
                            <li><hr class="dropdown-divider"></li>
                            <li><a class="dropdown-item" href="/Administration/Options">Einstellungen</a></li>
```

- [ ] **Step 2: Build and verify**

---

## Summary

| Layer | Files Created | Files Modified |
|---|---|---|
| Data | SystemOption.cs, OptionDefinition.cs, OptionRepository.cs | Chapter.cs, ChapterRepository.cs, M001 migration, DbContext.cs |
| API | OptionDefinitionDTO.cs, SystemOptionDTO.cs, OptionUpdateRequest.cs, TemplatePreviewRequest.cs, TemplatePreviewResponse.cs | — |
| Server | OptionListEndpoint.cs, OptionUpdateEndpoint.cs, TemplatePreviewEndpoint.cs, TemplateRenderer.cs, TemplateMockDataProvider.cs | — |
| Blazor | OptionList.razor/.cs | MainLayout.razor |
