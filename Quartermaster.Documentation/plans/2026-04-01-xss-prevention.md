# XSS Prevention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sanitize all HTML rendered via `(MarkupString)` in Blazor by adding profile-based HTML sanitization at the Markdown→HTML conversion layer, eliminating XSS vectors across motions, events, templates, and previews.

**Architecture:** A shared `MarkdownService` in the Api project wraps Markdig conversion + `HtmlSanitizer` sanitization. Two profiles control allowed tags: **Strict** (motions — formatting only, no links/tables) and **Standard** (events/templates — formatting + links + tables). All 5 duplicate `MarkdownPipeline` initializations are replaced with `MarkdownService` calls. Client-side (Blazor WASM) and server-side share the same code path.

**Tech Stack:** `HtmlSanitizer` NuGet (Ganss.Xss), Markdig, Fluid.Core

---

## File Structure

### New files

| File | Responsibility |
|---|---|
| `Quartermaster.Api/Rendering/SanitizationProfile.cs` | Enum: `Strict`, `Standard` |
| `Quartermaster.Api/Rendering/HtmlSanitizationService.cs` | Creates and caches `HtmlSanitizer` instances per profile, exposes `Sanitize(html, profile)` |
| `Quartermaster.Api/Rendering/MarkdownService.cs` | Wraps `Markdown.ToHtml()` + `HtmlSanitizationService.Sanitize()`, single `MarkdownPipeline` |
| `Quartermaster.Server.Tests/Rendering/HtmlSanitizationServiceTests.cs` | Tests sanitization rules for both profiles |
| `Quartermaster.Server.Tests/Rendering/MarkdownServiceTests.cs` | Tests end-to-end markdown→sanitized HTML |

### Modified files

| File | Change |
|---|---|
| `Quartermaster.Api/Quartermaster.Api.csproj` | Add `HtmlSanitizer` package |
| `Quartermaster.Api/Rendering/TemplateRenderer.cs` | Replace `Markdown.ToHtml()` with `MarkdownService.ToHtml(, Standard)` and remove local pipeline |
| `Quartermaster.Server/Motions/MotionCreateEndpoint.cs` | Replace `Markdown.ToHtml()` with `MarkdownService.ToHtml(, Strict)` and remove local pipeline |
| `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs` | Replace `Markdown.ToHtml()` with `MarkdownService.ToHtml(, Strict)` and remove local pipeline |
| `Quartermaster.Server/Events/ChecklistItemExecutor.cs` | Replace `Markdown.ToHtml()` with `MarkdownService.ToHtml(, Strict)` and remove local pipeline |
| `Quartermaster.Blazor/Components/Inputs/MarkdownEditor.razor.cs` | Add `Profile` parameter, use `MarkdownService.ToHtml()` instead of local pipeline |
| `Quartermaster.Blazor/Pages/Administration/EventDetail.razor` | Pass `Profile` to MarkdownEditor instances |
| `Quartermaster.Blazor/Pages/Administration/EventDetail.razor.cs` | Sanitize email preview HTML via `HtmlSanitizationService` |
| `Quartermaster.Blazor/Pages/Administration/MotionCreate.razor` | Pass `Profile="SanitizationProfile.Strict"` to MarkdownEditor |

---

## Sanitization Profiles

### Strict (Motions)

**Allowed tags:** `p`, `br`, `b`, `i`, `em`, `strong`, `u`, `s`, `del`, `sup`, `sub`, `ul`, `ol`, `li`, `h1`–`h6`, `blockquote`, `pre`, `code`, `hr`

**Allowed attributes:** `class` (on `code` and `pre` only, for syntax highlighting)

**Blocked:** `a`, `img`, `table`, `iframe`, `script`, `style`, all event handlers, all `data-*` attributes

### Standard (Events, Templates)

**Allowed tags:** Everything in Strict + `a`, `table`, `thead`, `tbody`, `tfoot`, `tr`, `th`, `td`, `caption`, `img`

**Allowed attributes:** Everything in Strict + `href`/`rel`/`target` on `a`, `src`/`alt`/`width`/`height` on `img`, `colspan`/`rowspan` on `td`/`th`

**Allowed URI schemes:** `https`, `http`, `mailto` (for `href` and `src`)

---

## Tasks

### Task 1: Add HtmlSanitizer package and create sanitization service

**Files:**
- Modify: `Quartermaster.Api/Quartermaster.Api.csproj`
- Create: `Quartermaster.Api/Rendering/SanitizationProfile.cs`
- Create: `Quartermaster.Api/Rendering/HtmlSanitizationService.cs`
- Test: `Quartermaster.Server.Tests/Rendering/HtmlSanitizationServiceTests.cs`

- [ ] **Step 1: Add HtmlSanitizer NuGet package to Api project**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet add Quartermaster.Api/Quartermaster.Api.csproj package HtmlSanitizer
```

- [ ] **Step 2: Create SanitizationProfile enum**

Create `Quartermaster.Api/Rendering/SanitizationProfile.cs`:

```csharp
namespace Quartermaster.Api.Rendering;

public enum SanitizationProfile {
    Strict,
    Standard
}
```

- [ ] **Step 3: Write the test file**

Create `Quartermaster.Server.Tests/Rendering/HtmlSanitizationServiceTests.cs`:

```csharp
using FluentValidation.TestHelper;
using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Tests.Rendering;

public class HtmlSanitizationServiceStrictTests {
    [Test]
    public void RemovesScriptTags() {
        var html = "<p>Hello</p><script>alert('xss')</script>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).Contains("<p>Hello</p>");
        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public void RemovesOnClickHandlers() {
        var html = "<p onclick=\"alert('xss')\">Click me</p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).Contains("<p>");
        await Assert.That(result).DoesNotContain("onclick");
    }

    [Test]
    public void RemovesLinks() {
        var html = "<p>Visit <a href=\"https://example.com\">here</a></p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<a");
        await Assert.That(result).DoesNotContain("href");
        await Assert.That(result).Contains("here");
    }

    [Test]
    public void RemovesTables() {
        var html = "<table><tr><td>data</td></tr></table>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<table");
        await Assert.That(result).DoesNotContain("<td");
        await Assert.That(result).Contains("data");
    }

    [Test]
    public void RemovesImages() {
        var html = "<p>Text</p><img src=\"https://evil.com/tracker.png\" />";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<img");
        await Assert.That(result).DoesNotContain("src");
    }

    [Test]
    public void AllowsFormattingTags() {
        var html = "<p><strong>bold</strong> <em>italic</em> <code>code</code></p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).Contains("<strong>bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
        await Assert.That(result).Contains("<code>code</code>");
    }

    [Test]
    public void AllowsHeadings() {
        var html = "<h1>Title</h1><h3>Subtitle</h3>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).Contains("<h1>Title</h1>");
        await Assert.That(result).Contains("<h3>Subtitle</h3>");
    }

    [Test]
    public void AllowsLists() {
        var html = "<ul><li>Item 1</li><li>Item 2</li></ul>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).Contains("<ul>");
        await Assert.That(result).Contains("<li>Item 1</li>");
    }

    [Test]
    public void RemovesStyleTags() {
        var html = "<style>body { display: none; }</style><p>Visible</p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<style");
        await Assert.That(result).Contains("<p>Visible</p>");
    }

    [Test]
    public void RemovesIframes() {
        var html = "<iframe src=\"https://evil.com\"></iframe><p>Safe</p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<iframe");
        await Assert.That(result).Contains("<p>Safe</p>");
    }
}

public class HtmlSanitizationServiceStandardTests {
    [Test]
    public void RemovesScriptTags() {
        var html = "<p>Hello</p><script>alert('xss')</script>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).DoesNotContain("<script>");
    }

    [Test]
    public void RemovesOnClickHandlers() {
        var html = "<a href=\"#\" onclick=\"alert('xss')\">Click</a>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).DoesNotContain("onclick");
        await Assert.That(result).Contains("<a");
    }

    [Test]
    public void AllowsLinks() {
        var html = "<p>Visit <a href=\"https://example.com\">here</a></p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).Contains("<a");
        await Assert.That(result).Contains("href=\"https://example.com\"");
    }

    [Test]
    public void AllowsTables() {
        var html = "<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Data</td></tr></tbody></table>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).Contains("<table>");
        await Assert.That(result).Contains("<th>Header</th>");
        await Assert.That(result).Contains("<td>Data</td>");
    }

    [Test]
    public void AllowsImages() {
        var html = "<img src=\"https://example.com/img.png\" alt=\"photo\" />";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).Contains("<img");
        await Assert.That(result).Contains("src=\"https://example.com/img.png\"");
    }

    [Test]
    public void BlocksJavascriptUrls() {
        var html = "<a href=\"javascript:alert('xss')\">Click</a>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).DoesNotContain("javascript:");
    }

    [Test]
    public void RemovesIframes() {
        var html = "<iframe src=\"https://evil.com\"></iframe>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).DoesNotContain("<iframe");
    }

    [Test]
    public void AllowsFormattingTags() {
        var html = "<p><strong>bold</strong> <em>italic</em></p>";
        var result = HtmlSanitizationService.Sanitize(html, SanitizationProfile.Standard);
        await Assert.That(result).Contains("<strong>bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: Compilation error — `HtmlSanitizationService` doesn't exist yet.

- [ ] **Step 5: Implement HtmlSanitizationService**

Create `Quartermaster.Api/Rendering/HtmlSanitizationService.cs`:

```csharp
using Ganss.Xss;

namespace Quartermaster.Api.Rendering;

public static class HtmlSanitizationService {
    private static readonly HtmlSanitizer StrictSanitizer = CreateStrictSanitizer();
    private static readonly HtmlSanitizer StandardSanitizer = CreateStandardSanitizer();

    public static string Sanitize(string html, SanitizationProfile profile) {
        if (string.IsNullOrEmpty(html))
            return html;

        var sanitizer = profile switch {
            SanitizationProfile.Strict => StrictSanitizer,
            SanitizationProfile.Standard => StandardSanitizer,
            _ => StrictSanitizer
        };

        return sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer CreateStrictSanitizer() {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();

        foreach (var tag in new[] {
            "p", "br", "b", "i", "em", "strong", "u", "s", "del",
            "sup", "sub", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6",
            "blockquote", "pre", "code", "hr"
        })
            sanitizer.AllowedTags.Add(tag);

        sanitizer.AllowedAttributes.Add("class");

        return sanitizer;
    }

    private static HtmlSanitizer CreateStandardSanitizer() {
        var sanitizer = CreateStrictSanitizer();

        foreach (var tag in new[] {
            "a", "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "img"
        })
            sanitizer.AllowedTags.Add(tag);

        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("rel");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("src");
        sanitizer.AllowedAttributes.Add("alt");
        sanitizer.AllowedAttributes.Add("width");
        sanitizer.AllowedAttributes.Add("height");
        sanitizer.AllowedAttributes.Add("colspan");
        sanitizer.AllowedAttributes.Add("rowspan");

        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("mailto");

        return sanitizer;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: All tests pass (existing 173 + new sanitization tests).

- [ ] **Step 7: Commit**

---

### Task 2: Create MarkdownService

**Files:**
- Create: `Quartermaster.Api/Rendering/MarkdownService.cs`
- Test: `Quartermaster.Server.Tests/Rendering/MarkdownServiceTests.cs`

**Context:** This wraps Markdig + HtmlSanitizationService into a single call. It replaces 5 separate `MarkdownPipeline` static fields scattered across the codebase. Lives in Api project so both Server and Blazor WASM can use it.

- [ ] **Step 1: Write the test file**

Create `Quartermaster.Server.Tests/Rendering/MarkdownServiceTests.cs`:

```csharp
using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Tests.Rendering;

public class MarkdownServiceTests {
    [Test]
    public async Task ConvertsBasicMarkdown() {
        var result = MarkdownService.ToHtml("**bold** and *italic*");
        await Assert.That(result).Contains("<strong>bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
    }

    [Test]
    public async Task StrictRemovesLinksFromMarkdown() {
        var result = MarkdownService.ToHtml("[click here](https://example.com)", SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<a");
        await Assert.That(result).DoesNotContain("href");
        await Assert.That(result).Contains("click here");
    }

    [Test]
    public async Task StandardKeepsLinksFromMarkdown() {
        var result = MarkdownService.ToHtml("[click here](https://example.com)", SanitizationProfile.Standard);
        await Assert.That(result).Contains("<a");
        await Assert.That(result).Contains("href=\"https://example.com\"");
    }

    [Test]
    public async Task StrictRemovesTablesFromMarkdown() {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var result = MarkdownService.ToHtml(md, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<table");
    }

    [Test]
    public async Task StandardKeepsTablesFromMarkdown() {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var result = MarkdownService.ToHtml(md, SanitizationProfile.Standard);
        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<td>1</td>");
    }

    [Test]
    public async Task StripsScriptInjectionInMarkdown() {
        var md = "Hello\n\n<script>alert('xss')</script>\n\nWorld";
        var result = MarkdownService.ToHtml(md, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).Contains("Hello");
        await Assert.That(result).Contains("World");
    }

    [Test]
    public async Task HandlesEmptyInput() {
        await Assert.That(MarkdownService.ToHtml("")).IsEqualTo("");
        await Assert.That(MarkdownService.ToHtml(null!)).IsEqualTo("");
    }

    [Test]
    public async Task DefaultProfileIsStandard() {
        var result = MarkdownService.ToHtml("[link](https://example.com)");
        await Assert.That(result).Contains("<a");
    }

    [Test]
    public async Task StrictRemovesImagesFromMarkdown() {
        var md = "![alt](https://example.com/img.png)";
        var result = MarkdownService.ToHtml(md, SanitizationProfile.Strict);
        await Assert.That(result).DoesNotContain("<img");
    }

    [Test]
    public async Task StandardKeepsImagesFromMarkdown() {
        var md = "![alt](https://example.com/img.png)";
        var result = MarkdownService.ToHtml(md, SanitizationProfile.Standard);
        await Assert.That(result).Contains("<img");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: Compilation error — `MarkdownService` doesn't exist yet.

- [ ] **Step 3: Implement MarkdownService**

Create `Quartermaster.Api/Rendering/MarkdownService.cs`:

```csharp
using Markdig;

namespace Quartermaster.Api.Rendering;

public static class MarkdownService {
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string ToHtml(string markdown, SanitizationProfile profile = SanitizationProfile.Standard) {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var raw = Markdown.ToHtml(markdown, Pipeline);
        return HtmlSanitizationService.Sanitize(raw, profile);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

---

### Task 3: Update server-side Markdown consumers

**Files:**
- Modify: `Quartermaster.Server/Motions/MotionCreateEndpoint.cs`
- Modify: `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`
- Modify: `Quartermaster.Server/Events/ChecklistItemExecutor.cs`
- Modify: `Quartermaster.Api/Rendering/TemplateRenderer.cs`

**Context:** Replace all direct `Markdown.ToHtml()` calls and local `MarkdownPipeline` fields with `MarkdownService.ToHtml()`. Motions use `Strict` profile. Template rendering uses `Standard` profile.

- [ ] **Step 1: Update MotionCreateEndpoint**

In `Quartermaster.Server/Motions/MotionCreateEndpoint.cs`:

Remove these lines:
```csharp
using Markdig;
```
and:
```csharp
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
```

Add:
```csharp
using Quartermaster.Api.Rendering;
```

Replace line 33:
```csharp
            Text = Markdown.ToHtml(req.Text, MarkdownPipeline),
```
with:
```csharp
            Text = MarkdownService.ToHtml(req.Text, SanitizationProfile.Strict),
```

- [ ] **Step 2: Update MembershipApplicationCreateEndpoint**

In `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`:

Remove:
```csharp
using Markdig;
```
and:
```csharp
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
```

Add:
```csharp
using Quartermaster.Api.Rendering;
```

Replace line 81:
```csharp
                Text = Markdown.ToHtml(md, MarkdownPipeline),
```
with:
```csharp
                Text = MarkdownService.ToHtml(md, SanitizationProfile.Strict),
```

- [ ] **Step 3: Update ChecklistItemExecutor**

In `Quartermaster.Server/Events/ChecklistItemExecutor.cs`:

Remove:
```csharp
using Markdig;
```
and:
```csharp
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
```

Add:
```csharp
using Quartermaster.Api.Rendering;
```

Replace line 43:
```csharp
            Text = Markdown.ToHtml(config.MotionText, MarkdownPipeline),
```
with:
```csharp
            Text = MarkdownService.ToHtml(config.MotionText, SanitizationProfile.Strict),
```

- [ ] **Step 4: Update TemplateRenderer**

In `Quartermaster.Api/Rendering/TemplateRenderer.cs`:

Remove:
```csharp
using Markdig;
```
and:
```csharp
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
```

Replace line 25:
```csharp
        var html = Markdown.ToHtml(rendered, MarkdownPipeline);
```
with:
```csharp
        var html = MarkdownService.ToHtml(rendered, SanitizationProfile.Standard);
```

- [ ] **Step 5: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Run all tests**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

---

### Task 4: Update MarkdownEditor component

**Files:**
- Modify: `Quartermaster.Blazor/Components/Inputs/MarkdownEditor.razor.cs`
- Modify: `Quartermaster.Blazor/Pages/Administration/EventDetail.razor`
- Modify: `Quartermaster.Blazor/Pages/Administration/MotionCreate.razor`

**Context:** MarkdownEditor currently has its own `MarkdownPipeline` and calls `Markdown.ToHtml()` directly without sanitization. Add a `Profile` parameter (defaults to `Standard`) and replace with `MarkdownService.ToHtml()`. Then update callers to pass the appropriate profile.

- [ ] **Step 1: Update MarkdownEditor.razor.cs**

Replace the entire content of `Quartermaster.Blazor/Components/Inputs/MarkdownEditor.razor.cs` with:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Rendering;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class MarkdownEditor {
    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public int Rows { get; set; } = 8;

    [Parameter]
    public SanitizationProfile Profile { get; set; } = SanitizationProfile.Standard;

    private string RenderedHtml = "";
    private CancellationTokenSource? _debounce;

    private async Task OnInput(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            RenderedHtml = MarkdownService.ToHtml(Value, Profile);
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    protected override void OnParametersSet() {
        if (!string.IsNullOrWhiteSpace(Value))
            RenderedHtml = MarkdownService.ToHtml(Value, Profile);
    }
}
```

- [ ] **Step 2: Update EventDetail.razor MarkdownEditor usages**

In `Quartermaster.Blazor/Pages/Administration/EventDetail.razor`:

Line 96 — event description (Standard profile, the default):
```razor
<MarkdownEditor Value="@Event.Description" ValueChanged="OnDescriptionChanged" Rows="6" />
```
No change needed — default is `Standard`.

Line 163 — motion text in checklist item (Strict profile):
Change:
```razor
<MarkdownEditor @bind-Value="EditingMotionText" Rows="4" />
```
to:
```razor
<MarkdownEditor @bind-Value="EditingMotionText" Rows="4" Profile="SanitizationProfile.Strict" />
```

Add `@using Quartermaster.Api.Rendering` at the top of the file if not already present.

- [ ] **Step 3: Update MotionCreate.razor**

In `Quartermaster.Blazor/Pages/Administration/MotionCreate.razor`:

Line 34 — change:
```razor
<MarkdownEditor @bind-Value="Text" Rows="8" />
```
to:
```razor
<MarkdownEditor @bind-Value="Text" Rows="8" Profile="SanitizationProfile.Strict" />
```

Add `@using Quartermaster.Api.Rendering` at the top of the file if not already present.

- [ ] **Step 4: Verify build**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

---

### Task 5: Sanitize email preview in EventDetail

**Files:**
- Modify: `Quartermaster.Blazor/Pages/Administration/EventDetail.razor.cs`

**Context:** The `LoadEmailPreview` method renders templates via `TemplateRenderer.RenderAsync()` and stores the HTML in `PreviewCache`. `TemplateRenderer` now sanitizes internally (Task 3), so the preview HTML is already sanitized. However, there are two hardcoded HTML strings that bypass `TemplateRenderer` — these need sanitization too: the "no template" hint (line 275) and the error messages (lines 283, 285). Since these are system-generated strings with no user input, they're safe. No changes needed beyond verifying `TemplateRenderer` integration.

- [ ] **Step 1: Verify TemplateRenderer covers the preview path**

Read `Quartermaster.Blazor/Pages/Administration/EventDetail.razor.cs` around line 282.

The call `await TemplateRenderer.RenderAsync(templateContent, mockData)` already goes through `TemplateRenderer` which now uses `MarkdownService.ToHtml(rendered, SanitizationProfile.Standard)`. The preview HTML is sanitized.

The hardcoded HTML strings at lines 275, 283, 285 are safe because they only contain static system text (no user input).

No code changes needed for this task — just verification.

- [ ] **Step 2: Verify build and tests still pass**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet build Quartermaster.Server/Quartermaster.Server.csproj && \
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: Build and all tests pass.

---

### Task 6: Drop database and restart

**Files:**
- None (operational task)

**Context:** Existing Motion.Text records in the database contain unsanitized HTML from before this change. Since this is only test data, dropping and recreating the database via the existing migration is the cleanest approach.

- [ ] **Step 1: Stop the running server**

```bash
pkill -f "Quartermaster.Server" 2>/dev/null
```

- [ ] **Step 2: Drop the database**

The server uses MySQL/MariaDB. The database name is in `appsettings.json`. Drop it and let the migration recreate on next startup.

Read `Quartermaster.Server/appsettings.json` (or `appsettings.Development.json`) to find the database name. Then:

```bash
mysql -u root -e "DROP DATABASE IF EXISTS <database_name>;"
```

Or if credentials are needed, read them from the connection string.

- [ ] **Step 3: Rebuild and start server**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet run --project Quartermaster.Server/Quartermaster.Server.csproj
```

Verify: Server starts, migration runs, tables recreated. Test by loading `http://localhost:5232` in browser.

- [ ] **Step 4: Run all tests one final time**

```bash
cd /media/SMB/Quartermaster
/usr/lib/dotnet/dotnet test --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: All tests pass.

---

## Checklist: Production Readiness TODOs Covered

| TODO | Status |
|---|---|
| Add HTML sanitization to all `(MarkupString)` usage in Blazor | ✅ All 4 locations covered via MarkdownService/TemplateRenderer |
| Sanitize Markdown→HTML output server-side before storing | ✅ MotionCreateEndpoint, MembershipApplicationCreateEndpoint, ChecklistItemExecutor |
| Consider using `HtmlSanitizer` NuGet package | ✅ Using Ganss.Xss.HtmlSanitizer with two profiles |
| Audit Fluid template rendering for injection risks | ✅ TemplateRenderer now sanitizes output via MarkdownService |
