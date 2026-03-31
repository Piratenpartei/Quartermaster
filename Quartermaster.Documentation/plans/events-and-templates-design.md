# Event & Email System — Design Spec

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** A system for managing events with typed checklists (text, create motion, send email), event templates with typed variables for recurring events, and a stubbed email sending infrastructure.

**Architecture:** Event entity with ordered checklist items stored as separate entities. Checklist items have a type (Text/CreateMotion/SendEmail) with JSON-serialized type-specific configuration. Event templates store raw text with `{{variable}}` placeholders and variable type definitions. Server-side execution of checklist actions (motion creation, email sending) via a single check endpoint.

**Tech Stack:** Existing stack — LinqToDB, FastEndpoints, Blazor WASM, Markdig for markdown rendering, existing options/template system for email templates.

---

## 1. Data Model

### 1.1 Event Entity

`Quartermaster.Data/Events/Event.cs`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| ChapterId | Guid | FK to Chapters, required — events always belong to a chapter |
| InternalName | string | Admin/system name |
| PublicName | string | Subject line / publicly displayed name |
| Description | string? | Markdown content |
| EventDate | DateTime? | When the event takes place, nullable for generic emails |
| IsArchived | bool | Soft archive flag |
| EventTemplateId | Guid? | FK to EventTemplates, null if created manually |
| CreatedAt | DateTime | |

### 1.2 EventChecklistItem Entity

`Quartermaster.Data/Events/EventChecklistItem.cs`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| EventId | Guid | FK to Events |
| SortOrder | int | Display ordering |
| ItemType | ChecklistItemType (enum) | Text = 0, CreateMotion = 1, SendEmail = 2 |
| Label | string | Display text |
| IsCompleted | bool | |
| CompletedAt | DateTime? | When checked off |
| Configuration | string? | JSON, type-specific config (see below) |
| ResultId | Guid? | ID of created entity (motion ID, future email log ID) |

**Configuration JSON by type:**

- **Text:** null (no config needed)
- **CreateMotion:** `{ "chapterId": "guid", "motionTitle": "string", "motionText": "string (markdown)" }`
- **SendEmail:** `{ "targetType": "Chapter" | "AdministrativeDivision", "targetId": "guid", "templateIdentifier": "string (option template identifier)" }`

### 1.3 EventTemplate Entity

`Quartermaster.Data/Events/EventTemplate.cs`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| Name | string | Template display name |
| PublicNameTemplate | string | With `{{variable}}` placeholders |
| DescriptionTemplate | string? | Markdown with `{{variable}}` placeholders |
| Variables | string | JSON array of variable definitions |
| ChecklistItemTemplates | string | JSON array of checklist item definitions |
| ChapterId | Guid? | FK, optional — if template is chapter-specific |
| CreatedAt | DateTime | |

**Variables JSON structure:**
```json
[
  { "name": "date", "label": "Datum der Veranstaltung", "type": "Date" },
  { "name": "location", "label": "Veranstaltungsort", "type": "Text" }
]
```

**Variable types:** Text, Date, Time, Number, OptionTemplate, Chapter

**ChecklistItemTemplates JSON structure:**
```json
[
  { "sortOrder": 0, "itemType": 0, "label": "Raum buchen", "configuration": null },
  { "sortOrder": 1, "itemType": 2, "label": "Einladung versenden", "configuration": "{...}" }
]
```

### 1.4 ChecklistItemType Enum

```csharp
public enum ChecklistItemType {
    Text = 0,
    CreateMotion = 1,
    SendEmail = 2
}
```

## 2. Checklist Item Execution

### 2.1 Single Endpoint

`POST /api/events/{eventId}/checklist/{itemId}/check` with body `{ "executeAction": true|false }`

Server-side handling:

- **Text:** Marks completed, ignores `executeAction`
- **CreateMotion + executeAction=true:** Parses config JSON, calls `MotionRepository.Create()` with chapter/title/text (markdown rendered via Markdig), stores returned motion ID in `ResultId`, marks completed
- **CreateMotion + executeAction=false:** Marks completed without creating anything
- **SendEmail + executeAction=true:** Parses config, resolves recipients (see Section 3), calls stubbed email service, marks completed
- **SendEmail + executeAction=false:** Marks completed without sending

### 2.2 Uncheck Endpoint

`POST /api/events/{eventId}/checklist/{itemId}/uncheck`

- Only works for `ItemType == Text`. Returns error for action items.
- Sets `IsCompleted = false`, clears `CompletedAt`.

### 2.3 Irreversibility

CreateMotion and SendEmail items cannot be unchecked once completed. The action (motion creation, email sending) is irreversible. `ResultId` persists for reference.

## 3. Email Sending

### 3.1 Recipient Resolution

**Target type "Chapter":** Load all members where `ChapterId` matches the target chapter or any descendant chapter (using existing `ChapterRepository.GetDescendantIds()`).

**Target type "AdministrativeDivision":** Load all members where `ResidenceAdministrativeDivisionId` matches the target division.

Both resolve to a list of Members with email addresses.

### 3.2 Stubbed Email Service

`MemberEmailService` — singleton service that:
1. Resolves recipients from target type + ID
2. Resolves the email template using the existing option template system (Fluid + Markdig) with each Member as the model
3. For now: logs what would be sent (recipient count, template used) instead of actually sending
4. Returns a summary (recipient count, any errors)

The template model is always `MemberDetailDTO` (aliased as `member` in Fluid), giving access to all member fields.

### 3.3 Email Preview (Client-Side)

Email checklist items have a collapsible preview section on the event detail page. The preview is rendered **client-side in Blazor WASM** — no server round-trip needed.

Implementation:
- Move `TemplateRenderer` and `TemplateMockDataProvider` (or shared versions) into `Quartermaster.Api` so both Server and Blazor can use them
- Add Fluid.Core and Markdig NuGet references to `Quartermaster.Api`
- The Blazor event detail page loads the option template value, renders it locally with a mock `MemberDetailDTO` via the shared `TemplateRenderer`, and displays the result in a collapsible card
- Toggled via an expand/collapse button on the checklist item

> **Future TODO:** The existing `OptionDetail` page's template preview currently uses `POST /api/options/preview` (server-side rendering via `TemplatePreviewEndpoint`). This should be migrated to use the same client-side rendering approach. Once migrated, the `TemplatePreviewEndpoint` can be removed.

## 4. Event Templates & Variables

### 4.1 Creating a Template from an Event

Flow triggered from event detail page ("Als Vorlage speichern"):

1. System scans all text fields for `{{...}}` patterns:
   - Event: PublicName, Description
   - Checklist items: Label, Configuration JSON string values
2. Extracts unique variable names
3. Presents a form: for each variable, user sets Label (display name) and Type (Text/Date/Time/Number/OptionTemplate/Chapter)
4. Saves EventTemplate with:
   - Raw PublicName/Description (variables still as `{{...}}`)
   - Variables JSON array with name/label/type
   - ChecklistItemTemplates JSON from the event's checklist items (with variables intact)

### 4.2 Creating an Event from a Template

Page: `/Administration/Events/CreateFromTemplate/{TemplateId}`

1. Loads template, displays variable definitions
2. Renders typed inputs for each variable:
   - Text → `<input type="text">`
   - Date → `<input type="date">` (browser date picker), formats as dd.MM.yyyy on replacement
   - Time → `<input type="time">` (browser time picker), formats as HH:mm on replacement
   - Number → `<input type="number">`
   - OptionTemplate → searchable dropdown of option templates
   - Chapter → chapter dropdown
3. User selects the chapter for the event
4. Live preview of resolved PublicName as variables are filled in
5. On submit: string-replace all `{{variable}}` in PublicName, Description, and all checklist item labels/configurations
6. Creates Event + EventChecklistItems with resolved text
7. Sets `EventTemplateId` on the Event
8. Redirects to event detail page

### 4.3 Variable Replacement

Simple string replacement: for each variable definition, replace all occurrences of `{{name}}` with the user-provided value. No logic, no loops, no conditionals. Replacement happens on:
- PublicName
- Description
- Each checklist item Label
- Each checklist item Configuration (raw JSON string replacement)

Replacement format by type:
| Type | Format |
|------|--------|
| Text | raw string |
| Date | dd.MM.yyyy |
| Time | HH:mm |
| Number | raw number string |
| OptionTemplate | the option template identifier string |
| Chapter | the chapter ID (Guid string) |

## 5. API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/events | Paginated list, filterable by chapter, `includeArchived` flag |
| POST | /api/events | Create event |
| GET | /api/events/{id} | Event detail with all checklist items |
| PUT | /api/events/{id} | Update event fields |
| POST | /api/events/{id}/archive | Toggle archive status |
| POST | /api/events/{id}/checklist | Add checklist item |
| PUT | /api/events/{id}/checklist/{itemId} | Update checklist item |
| DELETE | /api/events/{id}/checklist/{itemId} | Delete checklist item |
| POST | /api/events/{id}/checklist/{itemId}/check | Check item (body: `{ "executeAction": bool }`) |
| POST | /api/events/{id}/checklist/{itemId}/uncheck | Uncheck item (text items only) |
| GET | /api/eventtemplates | List all templates |
| POST | /api/eventtemplates | Create template from event |
| GET | /api/eventtemplates/{id} | Template detail |
| DELETE | /api/eventtemplates/{id} | Delete template |
| POST | /api/events/from-template | Create event from template with variable values |

## 6. Admin UI

### 6.1 Event List Page (`/Administration/Events`)

- Paginated table: Public Name, Chapter, Date, Progress (e.g. "3/5 items"), Created
- Chapter dropdown filter
- "Archivierte anzeigen" toggle
- Header buttons: "Neues Event" + "Aus Vorlage erstellen" (links to template list for selection)
- Navigation: link under "Vorstandsarbeit" dropdown

### 6.2 Event Detail Page (`/Administration/Events/{Id}`)

- Header: public name + chapter name + date + progress badge (e.g. "3/5")
- **Details card:** Internal name, description (rendered markdown), event date
- **Checklist card:** Ordered list of items:
  - **Text items (uncompleted):** Unchecked checkbox + label. Click to check.
  - **Text items (completed):** Checked checkbox + label. Click to uncheck.
  - **Action items (uncompleted):** Label + two buttons: "Erstellen & abhaken" / "Senden & abhaken" and "Bereits erledigt"
  - **Action items (completed):** Checked + label + link to result if ResultId set (e.g. "Zum Antrag")
  - **SendEmail items:** Collapsible preview section showing rendered email template with mock member data
- **Add checklist item form:** Type dropdown, label input, type-specific config fields (chapter picker, template search, etc.)
- **Footer buttons:** "Bearbeiten", "Als Vorlage speichern", "Archivieren"

### 6.3 Event Create Page (`/Administration/Events/Create`)

- Chapter picker, internal name, public name, description textarea (markdown), date picker
- On submit: creates event, redirects to detail page for adding checklist items

### 6.4 Create from Template Page (`/Administration/Events/CreateFromTemplate/{TemplateId}`)

- Template name displayed
- Chapter picker for the event
- Variable input form with typed inputs per variable definition
- Live preview of resolved public name
- Submit creates event + checklist items with resolved text, redirects to detail

### 6.5 Template List Page (`/Administration/EventTemplates`)

- Table: Name, Variable count, Checklist item count, Created
- Delete button per row
- Navigation: link under "Vorstandsarbeit" dropdown (or alongside events)

### 6.6 Save as Template Flow (from Event Detail)

- "Als Vorlage speichern" button on event detail page
- Navigates to a page showing:
  - Template name input (pre-filled from event internal name)
  - List of detected `{{variables}}` from all text fields
  - For each variable: Label input + Type dropdown
- Save creates the EventTemplate

### 6.7 Navigation

Under "Vorstandsarbeit" dropdown, add:
- "Events" → `/Administration/Events`
- "Event-Vorlagen" → `/Administration/EventTemplates`

## 7. Migration Changes

All in M001 (unreleased):
- Create Events table with all fields and FK to Chapters + EventTemplates
- Create EventChecklistItems table with FK to Events
- Create EventTemplates table with optional FK to Chapters
