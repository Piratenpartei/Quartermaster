# News Posts: Design

## Goal

Quartermaster watches news feeds, finds items that touch the programme of the Piratenpartei Deutschland, and uses the Mistral API to write short German post candidates grounded in that programme. A reviewer picks one candidate per news item, edits it, chooses Postiz channels and a time, and greenlights it. Quartermaster then publishes it through the self-hosted Postiz instance. Nothing is posted without that single-person greenlight.

V1 serves the federal level only. Chapters re-share federal posts in the social networks themselves. The data model carries a nullable `ChapterId` on every owned row so chapter-level feeds, programmes and channels can follow later without a schema redesign.

## Scope

In V1:

- Weekly programme sync from the Piratenwiki plus a manual trigger, split into sections, embedded with Mistral.
- Feed polling (RSS/Atom and the tagesschau JSON API), full-text extraction where the feed provides it.
- Embedding match against the programme, cheap LLM triage, candidate generation for relevant items, manual generation for any item.
- Review queue with edit, channel selection (live from Postiz), immediate or scheduled publishing.
- Feed and programme-source management, pipeline status.

Not in V1: chapter-level feeds, programmes or channels (only the `ChapterId` hooks); multi-person approval; images; HTML scraping for teaser-only feeds; saving edited candidates without posting; different texts per channel.

## Architecture

Code lives in `Quartermaster.Server/NewsPosts/` and `Quartermaster.Data/NewsPosts/`. Each unit has one job:

```
ProgrammeSyncService        Wiki pages -> sections -> embeddings (weekly + manual trigger)
NewsFeedPoller              Enabled feeds -> new NewsItems, dedup by ExternalId/Url
ArticleTextFetcher          Full text per item (RSS content:encoded, tagesschau details JSON)
ProgrammeMatcher            Item -> top 8 sections by cosine similarity over in-memory vectors
RelevanceTriage             Triage model: relevant yes/no + one-line reason (JSON)
CandidateGenerator          Generation model: article + passages -> candidates (JSON), validated
PostizClient                GET /public/v1/integrations, POST /public/v1/posts
MistralClient               Thin HTTP adapter for chat completions (JSON mode) and embeddings
NewsPipelineHostedService   Runs poll -> full text -> match -> triage -> generate on an interval
ProgrammeSyncHostedService  Runs the programme sync weekly
```

`MistralClient` and `PostizClient` are consumed through interfaces (`IMistralClient`, `IPostizClient`) so tests replace them with fakes. No Mistral SDK; plain `HttpClient` from the existing `AddHttpClient()` registration.

The section embeddings are held in a singleton in-memory index (`ProgrammeIndex`), loaded at startup and reloaded after every sync. Around 700 vectors of 1024 floats make a linear cosine scan cheap, so no vector database is used.

## Data Model

All tables are added to migration `M003` (V3 is not deployed yet).

```
ProgrammeSources     Id, ChapterId?, WikiPageTitle, LastSyncedAt?, LastRevisionId?, LastError?
ProgrammeSections    Id, SourceId, Heading, HeadingPath, Anchor, Body, ContentHash, Embedding (BLOB), UpdatedAt
NewsFeeds            Id, ChapterId?, Name, Url, Kind (Rss | TagesschauApi), IsEnabled, LastPolledAt?, LastError?
NewsItems            Id, FeedId, ExternalId, Url, Title, Teaser, ArticleText?, HasFullText, PublishedAt, FetchedAt,
                     Status, TriageReason?, BestSimilarity?, MatchedSectionIds (JSON), FailureCount, LastError?, UpdatedAt
PostCandidates       Id, NewsItemId, Text, SectionId, SortOrder, CreatedAt
PostPublications     Id, NewsItemId, CandidateId?, FinalText, ChannelIds (JSON), PostizPostIds (JSON),
                     ScheduledFor?, Status (Sent | Failed), Error?, ApprovedByUserId, ApprovedAt
```

`NewsItems.Status` values: `New`, `Irrelevant`, `Relevant`, `Generated`, `Posting`, `Posted`, `Dismissed`, `Failed`. Enums persist as ints and are append-only.

Unique indexes: `NewsItems (FeedId, ExternalId)`, `ProgrammeSources (ChapterId, WikiPageTitle)`.

`Embedding` stores the raw little-endian float array. `HeadingPath` is the joined heading chain (for example `Parteiprogramm > Innen- und Rechtspolitik > Jugendschutz`), `Anchor` the wiki anchor used to link to the section.

Seed data: the three V1 feeds (netzpolitik.org RSS, Bundestag hib RSS, tagesschau API) and the programme sources `Parteiprogramm`, `Bundestagswahl 2025/Wahlprogramm` and every `Positionspapiere/*` page listed at seed time.

## Programme Sync

1. For each `ProgrammeSource`, fetch the wikitext via `https://wiki.piratenpartei.de/api.php?action=parse&page=<title>&prop=wikitext|revid&format=json`. If the revision id equals `LastRevisionId`, skip the page.
2. Split the wikitext at `==` headings into sections with their heading path. Strip wiki markup to plain text (links, templates, bold/italic, references). Drop sections with an empty body.
3. Compute `ContentHash` over heading path plus body. Sections whose hash is unchanged keep their embedding; new or changed sections are embedded in batches; sections no longer present are deleted.
4. Each page syncs in its own transaction. A failed page keeps its previous sections and records `LastError`.
5. Reload `ProgrammeIndex`.

`ProgrammeSyncHostedService` runs the sync weekly. The settings page has a "Jetzt synchronisieren" button that triggers the same code path.

## News Pipeline

`NewsPipelineHostedService` runs every `newsposts.poll_interval_minutes` while `newsposts.enabled` is true and a Mistral key is configured. Each stage only picks up items in its input status, so a crash resumes on the next run.

1. **Poll:** fetch every enabled feed. Store new items (by `ExternalId`, falling back to `Url`) as `New`. Items older than `newsposts.max_item_age_hours` are ignored. A failing feed stores `LastError` and does not stop the others.
2. **Full text:** RSS items use `content:encoded` when present, otherwise the teaser. tagesschau items load their `details` JSON and concatenate the text paragraphs. `HasFullText` records which case applied.
3. **Match:** embed `Title + Teaser` for all new items of the run in one batched call, take the top 8 sections by cosine similarity, store their ids in `MatchedSectionIds` and the top score in `BestSimilarity`.
4. **Triage:** if `BestSimilarity` is below `newsposts.triage.min_similarity`, the item becomes `Irrelevant` without an LLM call. Otherwise the triage model receives title, teaser and the 8 passages and answers `{ "relevant": bool, "reason": string }`. The item becomes `Relevant` or `Irrelevant` with `TriageReason`.
5. **Generate:** for `Relevant` items, and on the manual "Generieren" action for any item. The generation model receives the article text (or teaser), the matched passages with their section ids and heading paths, the editable rule text from `newsposts.generation.prompt`, and fixed format instructions: German, at most 280 characters, `newsposts.generation.candidate_count` candidates, figures and quotes only from the article, each candidate cites one given section id, an empty list is a valid answer. The answer is `{ "candidates": [ { "text": string, "sectionId": string } ] }`. Candidates longer than 280 characters, or citing a section that was not in the input, are discarded. The item becomes `Generated` (possibly with zero candidates, shown as "Keine passenden Vorschläge"). Regenerating replaces the previous candidates.
6. **Errors:** Mistral calls retry with exponential backoff inside a run. An item whose stage fails in 3 consecutive runs becomes `Failed` with `LastError`; the UI offers "Erneut versuchen", which resets it to `New` with `FailureCount` 0 so every stage runs again.

LLM calls log model, token counts and duration, never the prompt text.

## Review Interface

Navigation: new entry "Social Media" under "Vorstandsarbeit".

`/Administration/NewsPosts` (permission `newsposts_review`), three tabs:

- **Vorschläge** (default): items in `Relevant` or `Generated`. Each item is a card with the headline linked to the source, feed name, publish time, a "Nur Teaser" badge when `HasFullText` is false, and the triage reason. Candidates are listed with a character counter and a collapsible "Programmstelle" showing the cited passage with a link to the wiki section. Actions: "Verwerfen" (to `Dismissed`), "Neu generieren".
- **Alle Meldungen:** all items with a status filter and the "Generieren" action on any item not yet posted.
- **Veröffentlicht:** publications with text, channels, time, approver and status; failed publications offer "Erneut versuchen", which reopens the greenlight form prefilled with the failed text, channels and time.

Greenlight flow: selecting a candidate opens it in an edit box with a live counter (red above 280). Below it the Postiz channels as checkboxes, loaded live; disabled channels are shown greyed out and cannot be selected. Then "Sofort" or a date and time, and "Freigeben & veröffentlichen". An in-page confirmation step precedes the request.

`/Administration/NewsPosts/Settings` (permission `newsposts_manage`): feed list with add, edit, enable/disable, last poll and last error; programme sources with add/remove, last sync, section count and "Jetzt synchronisieren"; a status panel with the last pipeline run, item counts per status, and connection checks for Mistral and Postiz.

## Posting to Postiz

On greenlight the server:

1. Checks the permission, text length 1 to 280, at least one channel, each channel present and not disabled in a fresh `GET /public/v1/integrations`, a future time when scheduled.
2. Claims the item with a conditional update from `Generated` to `Posting` (a greenlight always starts from a candidate). If no row changes, the request is rejected with a clear message; this also blocks double clicks.
3. Sends one `POST /public/v1/posts` with `type` `now` or `schedule`, `date` in UTC, `shortLink: false`, and one `posts[]` entry per channel with the same text and `settings.__type` set to the channel's `identifier`. X channels additionally get `who_can_reply_post` from `newsposts.postiz.x_reply_setting`.
4. On success stores a `PostPublications` row (`Sent`, Postiz post ids per channel, approver, time), sets the item to `Posted` and writes an audit log entry.
5. On failure (HTTP error or timeout) stores the publication as `Failed` with the error and sets the item back to `Generated`. There is no automatic retry, because a timeout may still have produced a public post; the reviewer checks Postiz and uses "Erneut versuchen".

The Postiz API key authenticates via the `Authorization` header. The create endpoint is rate-limited per instance (90 per hour by default), far above the expected volume.

## Permissions

- `newsposts_review`: view the section, generate, edit, greenlight, dismiss.
- `newsposts_manage`: manage feeds and programme sources, trigger the sync, view pipeline status.

Both are global permissions in V1 and are added to `SystemPermissions`.

## Configuration

Options, all global:

```
newsposts.enabled                          Master switch, default off
newsposts.poll_interval_minutes            Default 15
newsposts.max_item_age_hours               Default 48
newsposts.retention_days                   Default 30; items without a publication are deleted after this
newsposts.mistral.api_key                  Secret
newsposts.mistral.base_url                 Default https://api.mistral.ai
newsposts.mistral.triage_model             Default mistral-small-latest
newsposts.mistral.generation_model         Default mistral-medium-latest
newsposts.mistral.embedding_model          Default mistral-embed
newsposts.triage.min_similarity            Cosine floor before the triage call, default 0.70; tune after the first live run
newsposts.generation.candidate_count       Default 5
newsposts.generation.prompt                Editable tone and style rules, seeded from the spike examples
newsposts.postiz.base_url                  For example https://postiz.example.org/api
newsposts.postiz.api_key                   Secret
newsposts.postiz.x_reply_setting           Default everyone
```

## Testing

- Unit tests: wikitext section splitter (sample wikitext), RSS and tagesschau parsers (captured sample payloads), cosine matcher, candidate validation (length, unknown section id, empty list), Postiz request building per platform.
- `IMistralClient` and `IPostizClient` are faked in integration tests; no test calls Mistral or Postiz.
- Repository and endpoint tests follow the existing patterns, including the `Posting` claim under concurrent greenlights and permission checks on every endpoint.
- One manual end-to-end run against the real Mistral key and Postiz instance, preferably with a test channel, before enabling it in production.

## Documentation

- `Configuration.md` gets a "News Posts" section listing the options.
- `V3.md` gets a short German changelog entry for the new "Social Media" section.
