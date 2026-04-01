# Email/Messaging System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the stubbed MemberEmailService with actual SMTP email sending via MailKit, with SMTP config in the Options system, per-member Fluid template rendering, a background queue for non-blocking sends, and an EmailLog table tracking every send attempt with source traceability.

**Architecture:** SMTP settings stored as Options (admin UI configurable). `EmailService` replaces `MemberEmailService` — resolves recipients, renders personalized templates via Fluid, enqueues emails into a `Channel<EmailMessage>`. A `EmailSendingBackgroundService` dequeues and sends via MailKit with retry logic. Each send attempt (success/failure) is logged to an `EmailLog` table with source entity reference.

**Tech Stack:** MailKit, System.Threading.Channels, Fluid template engine, existing Options system

---

## Tasks

### Task 1: EmailLog entity, migration, DTO

**Files to create:**
- `Quartermaster.Data/Email/EmailLog.cs`
- `Quartermaster.Data/Email/EmailLogRepository.cs`
- `Quartermaster.Api/Email/EmailLogDTO.cs`

**EmailLog entity:**
- `Guid Id` (PK)
- `string Recipient` (email address)
- `string Subject`
- `string? TemplateIdentifier` (option key used, if any)
- `string? SourceEntityType` (e.g., "Event", "EventChecklistItem")
- `Guid? SourceEntityId` (e.g., event ID or checklist item ID)
- `string Status` ("Sent", "Failed", "Pending")
- `string? Error` (error message on failure)
- `int AttemptCount`
- `DateTime CreatedAt`
- `DateTime? SentAt`

**Add to M001 migration** (table + index on SourceEntityType+SourceEntityId + index on Status).

**Add to DbContext** (ITable + repository registration).

**EmailLogRepository methods:**
- `Create(EmailLog log)`
- `UpdateStatus(Guid id, string status, string? error, DateTime? sentAt)`
- `IncrementAttempt(Guid id)`
- `GetForSource(string entityType, Guid entityId)` — logs for a specific source entity
- `GetPending()` — all pending logs for retry
- `GetRecent(int count)` — recent logs for admin view

### Task 2: SMTP Options + EmailService

**Add SMTP option definitions** to `OptionRepository.SupplementDefaults()`:
- `email.smtp.host` — "SMTP: Server"
- `email.smtp.port` — "SMTP: Port" (default "587")
- `email.smtp.username` — "SMTP: Benutzername"
- `email.smtp.password` — "SMTP: Passwort"
- `email.smtp.sender_address` — "SMTP: Absenderadresse"
- `email.smtp.sender_name` — "SMTP: Absendername"
- `email.smtp.use_ssl` — "SMTP: SSL verwenden" (default "true")

**Create** `Quartermaster.Server/Email/EmailService.cs` replacing `MemberEmailService`:
- Constructor: `EmailLogRepository`, `OptionRepository`, `MemberRepository`, `ChapterRepository`, `Channel<EmailMessage>`, `ILogger`
- `SendEmail(targetType, targetId, templateIdentifier, descriptionOverride, manualAddresses, sourceEntityType, sourceEntityId)`:
  1. Resolve recipients (same logic as current stub but cleaner)
  2. Resolve template content (from Options or description override)
  3. For each recipient: render personalized template via `TemplateRenderer`, create `EmailLog` entry as "Pending", enqueue `EmailMessage` to the channel
  4. Return (count, error)

**Create** `Quartermaster.Server/Email/EmailMessage.cs` — simple record: `Guid EmailLogId, string To, string Subject, string HtmlBody, string? SourceEntityType, Guid? SourceEntityId`

### Task 3: Background sending service

**Create** `Quartermaster.Server/Email/EmailSendingBackgroundService.cs`:
- `BackgroundService` that reads from `Channel<EmailMessage>`
- For each message: connect to SMTP via MailKit, send, update EmailLog status
- Retry logic: on failure, increment attempt count, if < 3 attempts re-enqueue with delay, otherwise mark as "Failed"
- SMTP config read from Options on each send (or cached with short TTL)
- If SMTP not configured, log warning and mark as "Failed" with "SMTP nicht konfiguriert"

**Register** in Program.cs:
- `builder.Services.AddSingleton(Channel.CreateUnbounded<EmailMessage>())`
- `builder.Services.AddHostedService<EmailSendingBackgroundService>()`
- Replace `MemberEmailService` registration with `EmailService`

### Task 4: Update ChecklistItemExecutor + wire up

**Modify** `ChecklistItemExecutor.cs`:
- Replace `MemberEmailService` dependency with `EmailService`
- Pass `sourceEntityType: "EventChecklistItem"` and `sourceEntityId: item.Id` to `SendEmail`
- Pass the parent Event's Id as additional context

**Add** email log API endpoint: `GET /api/emaillogs?sourceEntityType=X&sourceEntityId=Y` returning `List<EmailLogDTO>`

### Task 5: Drop DB, rebuild, verify

Drop database, rebuild, run tests, start server, verify SMTP options appear in admin UI.

---

## Checklist

| TODO | Status |
|---|---|
| Implement actual SMTP email sending | ✅ MailKit via background service |
| Add SMTP configuration options | ✅ 7 options in Options system (admin UI) |
| Design message bus abstraction | Deferred — just EmailService for now, extract when second channel needed |
| Email queue/retry mechanism | ✅ Channel + BackgroundService + 3 retries |
| Email template rendering with per-member personalization | ✅ Fluid rendering per recipient |
| Email sending log | ✅ EmailLog table with source entity traceability |
| Outbox pattern | Deferred — in-memory Channel sufficient for now |
