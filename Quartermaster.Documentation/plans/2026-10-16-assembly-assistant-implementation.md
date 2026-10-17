# Assembly Assistant Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Versammlungsassistent for the online Bundesparteitag 2026.2: a Mumble bot that tracks request channels and moves members on the chair's click, a chair console with queues, suggestions and speaking timer, an OBS overlay, and OpenSlides poll automation.

**Architecture:** New `AssemblyAssistant` folders in Api (enums, DTOs, hub contracts), Data (two entities, repository, migration `M005`), Server (Mumble protocol client, pure session logic, session host singleton, SignalR hub, OpenSlides live client and poll automation, endpoints) and Blazor (setup page, console, read-only view, overlay). All rules live in pure classes that take the current time as a parameter and return the side effects to perform (moves, texts, OpenSlides actions), so they are unit-testable without sockets. The only IO owners are `MumbleConnection`, `OpenSlidesAutoupdateClient`, `HttpOpenSlidesClient` and `AssemblySessionHost`.

**Tech Stack:** .NET 10, FastEndpoints, SignalR, LinqToDB + FluentMigrator on MariaDB, Blazor WASM, Google.Protobuf + Grpc.Tools (only the protobuf compiler, no gRPC services), TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-16-assembly-assistant-design.md`

**Deadline:** phases 1 to 5 (spec Must + Should) ready for the dry run around 21.11.2026. Phases 6 and 7 (Could) only if time remains; the assembly works without them.

## Global Constraints

- Seven phases. Each phase ends with a checkpoint task: build, run that phase's test classes, restart the local server, stop and hand over to the user. Never start the next phase before the user says so.
- Code style: everything in `CLAUDE.md` applies (K&R braces, if-body on the next line, explicit access modifiers, `var`, fluent LINQ only, one class per file except DTO files, no `@code` in `.razor`, no `using` aliases, no region comments, `System.Threading.Lock`, mapping methods on the types).
- Comments: none by default. A `///` summary only for a member contract that is not obvious; a WHY note goes into a `//` comment at the code.
- Never run any git command. The user commits.
- Never run the full test suite. Build with `cd /home/joscha/Projects/Quartermaster && dotnet build Quartermaster.sln -m:2 -v:q -nologo`, run only the classes named in the task with `dotnet run --no-build --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`.
- Namespace and folder name is `AssemblyAssistant` everywhere (`Assembly` alone collides with `System.Reflection.Assembly`).
- Schema: the whole data model is created in Task 1 in `Quartermaster.Data/Migrations/M005.cs` with `[Migration(007)]`. Later phases do not add columns; a missing column goes into `M005` (V5 is unreleased) and the controller is told, because the developer database then has to be altered by hand.
- Datetime columns `.AsCustom("DATETIME(6)")`, long text `.AsCustom("MEDIUMTEXT")`, Guids `.AsGuid()`, enums `.AsInt32()`. Timestamps are stored and transported in UTC; the list closing time is entered in Europe/Berlin through the existing `BerlinTime`.
- Enums are persisted as int: only ever append values, never reorder.
- User-facing strings are German and go through `I18nKey` with entries in both `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json`. Mumble warning texts are options (German defaults), not i18n keys.
- Mumble: automated tests never contact the Bundesmumble; they use `FakeMumbleServer` (Task 4) on 127.0.0.1 or a fake `IMumbleConnection`. Manual runs against the Bundesmumble happen only at checkpoints, by the user or on the user's explicit go, and only in the test channel tree.
- OpenSlides: tests use fakes only. Nothing on the dev machine writes to the real OpenSlides.
- The bot never moves a member on its own. The only automatic move is the bot moving itself back to its home channel.
- Polls are always `pseudoanonymous` and always carry `entitled_group_ids = [delegates group id]`.
- No em or en dashes anywhere (code strings, docs, changelog); use a plain hyphen.
- Changelog: `Quartermaster.Server/Changelog/V5.md`, German, `### Heading` plus one paragraph per feature, user-visible changes only.
- Terms: the spec's terms table goes into `Quartermaster.Documentation/Translations.md` (Task 9).
- Permissions: `assembly_chair` (control) and `assembly_view` (read-only console and log export), both chapter-scoped, not inherited. Checks use `PermissionContext.Has(event.ChapterId, ...)`; `assembly_chair` implies `assembly_view`. The setup page needs global `options_edit`.

## Review Focus

- Two chairs click "Rederecht" on the same entry at the same moment: exactly one move is sent, the second command is rejected as stale. Pinned in Task 8 (`Stale_version_command_is_rejected`) and Task 6 (`Give_floor_twice_moves_once`).
- A member leaves "Contra" and comes back within the grace period after a reconnect with a new session id: same position as before. After the grace period: appended at the end. Pinned in Task 6 (`Rejoin_within_grace_keeps_position`, `Rejoin_after_grace_appends`).
- Someone moves a member into "Redner" by hand in Mumble: the tool treats it as "give the floor" from that member's queue and never moves them back. Pinned in Task 6 (`Manual_move_into_speaker_channel_consumes_request`).
- The speaking timer after a Quartermaster restart in the middle of a speech: resumes from the persisted start, not from zero. Pinned in Task 7 (`Recovery_resumes_timer_from_log`).
- Auto stop sends exactly one `poll.stop` per poll even though the tick runs every 250 ms and the autoupdate state lags behind. Pinned in Task 14 (`Auto_stop_sends_stop_once`).
- 2/3 evaluation with abstentions: 34 Ja, 17 Nein, 20 Enthaltung is "2/3 erreicht"; 33 Ja, 17 Nein is not. Pinned in Task 13.
- A malformed Mumble frame (length beyond the limit, truncated varint) drops the connection and reconnects, never crashes the process. Pinned in Task 2 and Task 4.

## File Structure

```
Quartermaster.Api/AssemblyAssistant/
  ChannelRole.cs, QueueKind.cs, AssemblyPhase.cs, AssemblyItemKind.cs, AssemblyLogKind.cs, PollEvaluationKind.cs   enums
  AssemblySnapshotDTO.cs           full console state (one file, DTO group)
  AssemblyOverlayDTO.cs            public overlay state, no ids beyond what the overlay needs
  AssemblyCommandResult.cs         hub command answer
  AssemblyHubMethods.cs            hub method and client event names
  AssemblySetupDTO.cs              setup page (channels from the server, mapping, connection check result)
Quartermaster.Data/AssemblyAssistant/
  AssemblySession.cs, AssemblyLogEntry.cs, AssemblySessionRepository.cs
Quartermaster.Data/Migrations/M005.cs
Quartermaster.Server/AssemblyAssistant/
  Mumble/Mumble.proto              copy of mumble-voip/mumble 1.3.4 src/Mumble.proto, licence header kept
  Mumble/MumbleMessageType.cs, MumbleFrameCodec.cs, MumbleVarint.cs, MumbleVoicePacket.cs
  Mumble/MumbleServerModel.cs      channels, links, users; applies ChannelState/UserState/UserRemove
  Mumble/SpeakingTracker.cs        voice packets -> speaking start/stop with hangover
  Mumble/TextMessageQueue.cs       1 message per second
  Mumble/IMumbleConnection.cs, MumbleConnection.cs, MumbleEvent.cs, MumbleMoveResult.cs
  Mumble/MumbleSettings.cs, MumbleCertificateGenerator.cs, MumbleConnectionCheck.cs
  Session/AssemblySessionState.cs  pure state + rules, returns AssemblyEffects
  Session/SpeakerQueue.cs, QueueEntry.cs, GraceStore.cs, SpeakingTimer.cs, AssemblyEffects.cs
  Session/AssemblyTimes.cs         default times read from options
  Session/AssemblySessionHost.cs   singleton: Lock, tick, IO, persistence, snapshot publishing
  Session/AssemblySessionRecovery.cs
  Flow/SuggestionEngine.cs, AssemblyFlow.cs, GoMotionInterruption.cs        (phase 5)
  OpenSlides/OpenSlidesAutoupdateClient.cs, OpenSlidesKeyStore.cs, OpenSlidesSubscription.cs,
  OpenSlides/OpenSlidesMeetingView.cs, PollEvaluation.cs, PollAutomation.cs, OpenSlidesPollCreate.cs   (phase 4, 6)
  Templates/AssemblyPollTemplates.cs, ProceduralTopics.cs, ElectionFlow.cs   (phase 6)
  Export/AssemblyLogExport.cs                                               (phase 7)
  AssemblyHub.cs
  Endpoints/*                      setup, session start/end, log export
Quartermaster.Blazor/Pages/AssemblyAssistant/
  AssemblySetup.razor(.cs), AssemblyConsole.razor(.cs), AssemblyOverlay.razor(.cs)
Quartermaster.Blazor/Components/AssemblyAssistant/
  QueueColumn, SpeakerPanel, PollPanel, LogPanel, UserPicker
Quartermaster.Blazor/Layout/OverlayLayout.razor(.cs)
Quartermaster.Blazor/Services/AssemblyHubClient.cs
Quartermaster.Blazor/wwwroot/fonts/PoliticsHead.otf, DejaRip.otf, DejaRip-Bold.otf
Quartermaster.Blazor/wwwroot/css/overlay.css
Quartermaster.Server.Tests/AssemblyAssistant/*                 pure logic + repository tests
Quartermaster.Server.Tests/AssemblyAssistant/Fakes/FakeMumbleServer.cs, FakeMumbleConnection.cs
Quartermaster.Server.Tests/Integration/AssemblyAssistant/*     hub + endpoint tests
```

Patterns to copy (read these before writing the matching code):

- Entity, migration, repository with audit: `Quartermaster.Data/AssemblyMotions/AssemblyMotionPortal.cs`, `Quartermaster.Data/Migrations/M004.cs`, `AssemblyMotionPortalRepository.cs`; table and repository registration in `Quartermaster.Data/DbContext.cs`.
- Options seeding and settings record: `OptionRepository.SupplementDefaults` (`AddDefinitionIfNotExists(..., isSecret: true)`), `Quartermaster.Server/AssemblyMotions/OpenSlides/OpenSlidesSettings.cs` (record with masked `ToString`).
- Permission and system role seeding: the `ManageAssemblyMotions` / `MotionCommission` blocks in `PermissionIdentifier.cs`, `PermissionRepository.SupplementDefaults`, `RoleRepository.SupplementDefaults`.
- Hub with anonymous read and permission-checked writes: `Quartermaster.Server/Meetings/MeetingHub.cs`, mapped in `Program.ConfigureMiddleware`; hub tests `Quartermaster.Server.Tests/Integration/Meetings/MeetingHubTests.cs` (`Factory.Server.CreateHandler()`).
- Blazor hub client: `Quartermaster.Blazor/Services/MeetingHubClient.cs`.
- OpenSlides client and recording handler: `HttpOpenSlidesClient.cs`, `Quartermaster.Server.Tests/AssemblyMotions/Fakes/RecordingOpenSlidesHandler.cs`, `HttpOpenSlidesClientTests.cs`.
- Background service loop: `Quartermaster.Server/AssemblyMotions/AssemblyMotionDeadlineHostedService.cs`.
- Endpoints: `Quartermaster.Server/Accreditation/*` (global permission check, settings read).
- Blazor: admin page `Pages/Administration/EventDetail.razor(.cs)`, nav `Layout/MainNavBar.razor`, layout `Layout/FluidLayout.razor` with `BootingLayoutBase`.

---

# Phase 1: Mumble Connection

### Task 1: Schema, enums, permissions, options, packages

**Files:**
- Create: `Quartermaster.Api/AssemblyAssistant/ChannelRole.cs`, `QueueKind.cs`, `AssemblyPhase.cs`, `AssemblyItemKind.cs`, `AssemblyLogKind.cs`, `PollEvaluationKind.cs`
- Create: `Quartermaster.Data/AssemblyAssistant/AssemblySession.cs`, `AssemblyLogEntry.cs`
- Create: `Quartermaster.Data/Migrations/M005.cs`
- Modify: `Quartermaster.Data/DbContext.cs`, `Quartermaster.Api/PermissionIdentifier.cs`, `PermissionRepository.cs`, `RoleRepository.cs`, `OptionRepository.cs`
- Modify: `Quartermaster.Server/Quartermaster.Server.csproj` (`Google.Protobuf`, `Grpc.Tools` with `PrivateAssets="all"`, `<Protobuf Include="AssemblyAssistant/Mumble/Mumble.proto" GrpcServices="None" />`); use the newest stable versions on nuget.org that are at least two weeks old
- Test: `Quartermaster.Server.Tests/AssemblyAssistant/AssemblySeedingTests.cs`

**Interfaces:**

```csharp
public enum ChannelRole { Chair = 0, Speaker = 1, MotionAuthor = 2, Pro = 3, Contra = 4, Question = 5, Point = 6, Listener = 7 }
public enum QueueKind { Point = 0, Question = 1, Pro = 2, Contra = 3 }          // display order in the console
public enum AssemblyItemKind { Other = 0, Motion = 1, Election = 2, GeneralDebate = 3 }
public enum AssemblyPhase {
    None = 0, Presentation = 1, Questions = 2, Debate = 3, RoundEnd = 4, Statement = 5, ClosingWord = 6, Vote = 7,
    Candidacies = 8, CandidatePresentation = 9, CandidateQuestions = 10, ElectionRunning = 11, GeneralDebate = 12
}
public enum AssemblyLogKind { SessionStarted = 0, SessionEnded = 1, Speech = 2, ChairAction = 3, PhaseChange = 4, Poll = 5, ItemChange = 6 }
public enum PollEvaluationKind { NumbersOnly = 0, Simple = 1, TwoThirds = 2, Threshold = 3, Acceptance = 4 }
```

- `AssemblySession` (`AssemblySessions`): `Id`, `EventId`, `OpenSlidesMeetingId` (int?), `StartedAt`, `StartedByUserId`, `EndedAt` (DateTime?), `LogDeletedAt` (DateTime?).
- `AssemblyLogEntry` (`AssemblyLogEntries`): `Id`, `SessionId`, `At`, `Kind`, `ItemTitle` (string?, 500), `OpenSlidesAgendaItemId` (int?), `MumbleName` (string?, 200), `Role` (string?, 50), `StartedAt` (DateTime?), `EndedAt` (DateTime?), `AllottedSeconds` (int?), `OpenSlidesPollId` (int?), `Detail` (string?, MEDIUMTEXT), `ActorUserId` (Guid?).
- Migration: FK `EventId` -> `Events.Id`, `SessionId` -> `AssemblySessions.Id`; index `(SessionId, At)`; index on `AssemblySessions.EndedAt`.
- Permissions: `AssemblyChair = "assembly_chair"` ("Versammlungsleitung"), `AssemblyView = "assembly_view"` ("Versammlung mitlesen"), both `global: false`. System roles `assembly_chair` ("Versammlungsleitung", both permissions) and `assembly_minutes` ("Protokoll Versammlung", `assembly_view`), `ChapterScoped`, `InheritsToChildren = false`.
- Options (all `String` unless noted, German display names and one-line descriptions):
  - `mumble.host` (default `mumble.piratenpartei.de`), `mumble.port` (`64738`), `mumble.username` (`Versammlungsassistent`), `mumble.certificate` (secret, PKCS#12 Base64), `mumble.certificate_password` (secret), `mumble.server_certificate_sha256` (empty = system trust; set = pin, for a self-signed server certificate)
  - `mumble.channels.chair`, `.speaker`, `.motion_author`, `.pro`, `.contra`, `.question`, `.point`, `.listener` (channel ids as text, set from the setup page)
  - `assembly.times.presentation` (300), `.speech` (180), `.question` (30), `.statement` (60), `.closing_word` (180), `.candidate_presentation` (300), `.candidate_presentation_repeat` (180), `.candidate_question` (30), `.candidate_answer` (120); seconds
  - `assembly.grace_seconds` (30), `assembly.voice_hangover_ms` (500), `assembly.start_now_after_seconds` (20)
  - `assembly.messages.warning` (`Noch 30 Sekunden`), `assembly.messages.expired` (`Die Redezeit ist abgelaufen`)
  - `assembly.openslides.delegates_group_id`, `assembly.poll.duration_seconds` (70), `assembly.poll.auto_start` (`true`), `assembly.poll.auto_stop` (`true`), `assembly.poll.auto_publish` (`true`)

- [ ] **Step 1:** Enums, entities, migration, DbContext tables, permissions, roles, options.
- [ ] **Step 2:** Copy `Mumble.proto` from tag 1.3.4 (`https://raw.githubusercontent.com/mumble-voip/mumble/1.3.4/src/Mumble.proto`), set `option csharp_namespace = "Quartermaster.Server.AssemblyAssistant.Mumble.Proto";`, add the packages; the build must generate the classes.
- [ ] **Step 3: Tests** `AssemblySeedingTests : RepositoryTestBase`: `Permissions_are_chapter_scoped`, `Chair_role_has_both_permissions`, `Certificate_options_are_secret`, `Log_entry_round_trips` (insert and read a speech entry with all fields).
- [ ] **Step 4:** Build, run `AssemblySeedingTests`. Expected: PASS.

### Task 2: Frame codec, varint, voice packet

**Files:**
- Create: `Mumble/MumbleMessageType.cs`, `MumbleFrameCodec.cs`, `MumbleVarint.cs`, `MumbleVoicePacket.cs`
- Test: `MumbleFrameCodecTests.cs`, `MumbleVoicePacketTests.cs`

**Interfaces:**

```csharp
public enum MumbleMessageType : ushort { Version = 0, UdpTunnel = 1, Authenticate = 2, Ping = 3, Reject = 4, ServerSync = 5,
    ChannelRemove = 6, ChannelState = 7, UserRemove = 8, UserState = 9, BanList = 10, TextMessage = 11, PermissionDenied = 12,
    Acl = 13, QueryUsers = 14, CryptSetup = 15, ContextActionModify = 16, ContextAction = 17, UserList = 18, VoiceTarget = 19,
    PermissionQuery = 20, CodecVersion = 21, UserStats = 22, RequestBlob = 23, ServerConfig = 24, SuggestConfig = 25 }

public static class MumbleFrameCodec {
    public const int MaxPayloadLength = 8 * 1024 * 1024;
    public static async Task<(MumbleMessageType Type, byte[] Payload)> ReadAsync(Stream stream, CancellationToken ct)   // throws MumbleProtocolException
    public static async Task WriteAsync(Stream stream, MumbleMessageType type, IMessage message, CancellationToken ct)
}

public static class MumbleVarint {
    public static bool TryRead(ReadOnlySpan<byte> data, ref int offset, out ulong value)
}

public readonly record struct MumbleVoicePacket(uint SenderSession, byte Context, bool IsTerminator) {
    public static bool TryParse(ReadOnlySpan<byte> tunnelPayload, out MumbleVoicePacket packet)
}
```

- Frame: 2 bytes big-endian type, 4 bytes big-endian length, payload. Unknown types are returned, not rejected. A length above `MaxPayloadLength` throws.
- Varint (Mumble prefix coding, not LEB128): `0xxxxxxx` 7 bit; `10xxxxxx` + 1 byte 14 bit; `110xxxxx` + 2 bytes 21 bit; `1110xxxx` + 3 bytes 28 bit; `111100__` + 4 bytes 32 bit; `111101__` + 8 bytes 64 bit; `111110__` negative recursive; `111111xx` byte-inverted negative 2 bit. Truncation returns false.
- Voice: header byte, codec = `header >> 5`, context = `header & 0x1f`; codec 1 (Ping) is ignored; then session varint, sequence varint; for codec 4 (Opus) the next varint is `size`, terminator = `(size & 0x2000) != 0`. For CELT/Speex (codecs 0, 2, 3) frames are `byte` with continuation bit `0x80`; terminator = the last frame header is `0x00` (length 0). Only header, session and the terminator flag are extracted.
- [ ] **Step 1: Tests:** `Frame_round_trips`, `Oversized_length_throws`, `Truncated_header_throws_end_of_stream`, varint vectors for every prefix class (values 0, 127, 128, 16383, 16384, 2^21-1, 2^28, 2^32-1, 2^40), `Truncated_varint_returns_false`, `Opus_packet_reads_session_and_terminator` (hand-built bytes), `Opus_packet_without_terminator`, `Ping_codec_is_rejected`, `Listen_context_is_reported`.
- [ ] **Step 2:** Implement, build, run both classes. Expected: PASS.

### Task 3: Server model, speaking tracker, text queue

**Files:**
- Create: `Mumble/MumbleServerModel.cs`, `SpeakingTracker.cs`, `TextMessageQueue.cs`, `MumbleEvent.cs`
- Test: `MumbleServerModelTests.cs`, `SpeakingTrackerTests.cs`, `TextMessageQueueTests.cs`

**Interfaces:**

```csharp
public record MumbleChannel(uint Id, uint? ParentId, string Name, IReadOnlySet<uint> Links);
public record MumbleUser(uint Session, string Name, uint ChannelId, int? UserId, string? CertificateHash, bool SelfMute, bool SelfDeaf);
public record MumbleServerSnapshot(uint? OwnSession, IReadOnlyList<MumbleChannel> Channels, IReadOnlyList<MumbleUser> Users);

public abstract record MumbleEvent;
public record MumbleUserEntered(MumbleUser User, uint? PreviousChannelId) : MumbleEvent;    // also on first sight after sync
public record MumbleUserDisconnected(MumbleUser User) : MumbleEvent;
public record MumbleSpeakingStarted(uint Session, DateTime AtUtc) : MumbleEvent;
public record MumbleSpeakingStopped(uint Session, DateTime AtUtc) : MumbleEvent;
public record MumblePermissionDenied(uint? Session, uint? ChannelId, string Reason) : MumbleEvent;
public record MumbleConnectionChanged(MumbleConnectionState State, string? Error) : MumbleEvent;
public record MumbleSynced(MumbleServerSnapshot Snapshot) : MumbleEvent;
public record MumbleSelfMovedBack(uint FromChannelId) : MumbleEvent;
public enum MumbleConnectionState { Disconnected = 0, Connecting = 1, Synced = 2, Reconnecting = 3 }

public class MumbleServerModel {
    public IReadOnlyList<MumbleEvent> Apply(ChannelState message)
    public IReadOnlyList<MumbleEvent> Apply(ChannelRemove message)
    public IReadOnlyList<MumbleEvent> Apply(UserState message)
    public IReadOnlyList<MumbleEvent> Apply(UserRemove message)
    public IReadOnlyList<MumbleEvent> ApplyServerSync(ServerSync message)
    public MumbleServerSnapshot Snapshot()
}

public class SpeakingTracker {
    public SpeakingTracker(TimeSpan hangover)
    public MumbleEvent? OnPacket(MumbleVoicePacket packet, DateTime nowUtc)     // Started on first packet, Stopped on terminator
    public IReadOnlyList<MumbleEvent> OnTick(DateTime nowUtc)                    // Stopped for senders silent longer than hangover
}

public class TextMessageQueue {
    public void Enqueue(uint session, string message)
    public (uint Session, string Message)? DequeueDue(DateTime nowUtc)          // at most one per second
}
```

- UserState is a delta keyed by `session`; a missing `channel_id` in a user's first UserState means channel 0 (root). `links`, `links_add`, `links_remove` of ChannelState update the link sets on both sides. Events before ServerSync are collected but `MumbleUserEntered` for the initial list is only raised in `ApplyServerSync`, as part of `MumbleSynced`.
- [ ] **Step 1: Tests:** `Initial_user_without_channel_is_in_root`, `UserState_delta_keeps_unset_fields`, `Move_raises_entered_with_previous_channel`, `UserRemove_raises_disconnected`, `Links_add_and_remove_are_symmetric`, `Hash_is_kept`; tracker `First_packet_starts`, `Terminator_stops`, `Silence_longer_than_hangover_stops`, `Packet_within_hangover_does_not_restart`; queue `Second_message_waits_one_second`, `Order_is_kept`.
- [ ] **Step 2:** Implement, build, run the three classes. Expected: PASS.

### Task 4: MumbleConnection with fake server

**Files:**
- Create: `Mumble/IMumbleConnection.cs`, `MumbleConnection.cs`, `IMumbleConnectionFactory.cs`, `MumbleConnectionFactory.cs`, `MumbleMoveResult.cs`, `MumbleSettings.cs`, `MumbleProtocolException.cs`
- Create: `Quartermaster.Server.Tests/AssemblyAssistant/Fakes/FakeMumbleServer.cs`
- Test: `MumbleConnectionTests.cs` (`[NotInParallel]`)

**Interfaces:**

```csharp
public record MumbleSettings(string Host, int Port, string Username, string CertificateBase64, string CertificatePassword,
    string? ServerCertificateSha256, IReadOnlyDictionary<ChannelRole, uint> Channels) {
    public bool IsConfigured()
    public static MumbleSettings ReadFrom(OptionRepository options)
    public override string ToString()                 // without certificate and password
}

public enum MumbleMoveOutcome { Moved = 0, PermissionDenied = 1, ChannelFull = 2, Timeout = 3, NotConnected = 4, UnknownUser = 5 }
public record MumbleMoveResult(MumbleMoveOutcome Outcome, string? Detail);

public interface IMumbleConnection {
    MumbleConnectionState State { get; }
    event Action<MumbleEvent>? EventRaised;            // raised on the read loop thread
    MumbleServerSnapshot Snapshot();
    Task StartAsync(MumbleSettings settings, uint homeChannelId, CancellationToken ct);   // returns at once, keeps reconnecting until StopAsync
    Task StopAsync();
    Task<MumbleMoveResult> MoveAsync(uint session, uint channelId, CancellationToken ct);
    void QueueText(uint session, string message);
    Task<bool> RegisterSelfAsync(CancellationToken ct);
}

public interface IMumbleConnectionFactory {
    IMumbleConnection Create();          // tests swap the factory for one returning FakeMumbleConnection
}
```

Behaviour:
- TLS via `SslStream` with the PKCS#12 client certificate (`X509CertificateLoader.LoadPkcs12`); server validation: system trust, or if `ServerCertificateSha256` is set, accept exactly that SHA-256 fingerprint (case-insensitive hex, colons ignored).
- Handshake: read server Version, send `Version { version_v1 = 0x00010400, release = "Quartermaster", os = "Linux" }`, `Authenticate { username, opus = true, client_type = 1 }`. Collect state until `ServerSync`, then raise `MumbleSynced`. `Reject` ends the attempt with the reason in `MumbleConnectionChanged`.
- After sync: if the bot is not in `homeChannelId`, move itself there (`UserState { session = own, channel_id = home }`). Self-mute on (`self_mute = true`, `self_deaf = false`). Whenever a later UserState shows the bot elsewhere, move back and raise `MumbleSelfMovedBack`.
- Ping every 10 s with `timestamp`; four unanswered pings or any `MumbleProtocolException` -> close socket, `Reconnecting`, backoff 1, 2, 4, 8, 16, 30, 30 s. `StopAsync` ends everything and leaves `Disconnected`.
- `UdpTunnel` payloads go through `MumbleVoicePacket.TryParse` and `SpeakingTracker`; a 250 ms internal tick calls `SpeakingTracker.OnTick` and drains `TextMessageQueue`.
- `MoveAsync` sends the UserState and waits up to 3 s for a UserState of that session with the new channel (`Moved`), a `PermissionDenied` for it (`PermissionDenied` or `ChannelFull`), or times out.
- `RegisterSelfAsync` sends `UserState { session = own, user_id = 0 }` and waits up to 3 s for the own UserState with `user_id` set.

`FakeMumbleServer`: `TcpListener` on 127.0.0.1 port 0 with a self-signed server certificate created in the test, `SslStream` server side, scripted: sends Version, channels, users, ServerSync; records received frames; can push UserState/UserRemove/UdpTunnel, answer pings or stay silent, answer moves with UserState or PermissionDenied, send garbage.

- [ ] **Step 1: Tests:** `Handshake_sends_version_and_authenticate`, `Sync_raises_snapshot_with_users`, `Bot_moves_itself_to_home_channel`, `Bot_moves_back_when_moved_away`, `Move_confirmed_by_userstate`, `Move_permission_denied_is_reported`, `Move_without_answer_times_out`, `Voice_packet_raises_speaking_started_and_stopped`, `Text_messages_are_rate_limited`, `Missing_pongs_trigger_reconnect`, `Garbage_frame_triggers_reconnect`, `Pinned_server_certificate_is_accepted`, `Wrong_pin_is_rejected`, `Stop_disconnects_and_stays_down`.
- [ ] **Step 2:** Implement, build, run `MumbleConnectionTests`. Expected: PASS.

### Task 5: Setup page, certificate, connection check, docs

**Files:**
- Create: `Mumble/MumbleCertificateGenerator.cs`, `Mumble/MumbleConnectionCheck.cs`
- Create: `Endpoints/AssemblySetupGetEndpoint.cs`, `AssemblySetupCheckEndpoint.cs`, `AssemblySetupGenerateCertificateEndpoint.cs`, `AssemblySetupRegisterBotEndpoint.cs`, `AssemblySetupSaveChannelsEndpoint.cs`
- Create: `Quartermaster.Api/AssemblyAssistant/AssemblySetupDTO.cs`
- Create: `Quartermaster.Blazor/Pages/AssemblyAssistant/AssemblySetup.razor(.cs)`
- Modify: `Program.cs` (singleton `IMumbleConnectionFactory` -> `MumbleConnectionFactory`; the check creates its own short-lived connection through it), `MainNavBar.razor` (Administration > "Versammlungsassistent", visible with `options_edit`)
- Modify: `Quartermaster.Documentation/Configuration.md` (section "Versammlungsassistent": options, certificate, registration, ACL the user sets up: bot registered, Move on "Bundesparteitag" with "apply to subchannels", home channel)
- Test: `Integration/AssemblyAssistant/AssemblySetupEndpointTests.cs`, `MumbleCertificateGeneratorTests.cs`

Behaviour:
- Generate: self-signed RSA 2048, subject `CN=<mumble.username>`, valid 10 years, exported as PKCS#12 with a random password; both stored in the secret options. Refused if a certificate already exists unless `replace = true` (a new certificate makes the bot a new user in Mumble).
- Check: connects with the stored options, waits for sync (10 s), returns server version, own session, registered yes/no, channel tree (id, parent, name, links) and the current mapping with problems (unmapped role, channel missing, Speaker/MotionAuthor/Listener not linked with the Chair channel); disconnects.
- Register: connect, `RegisterSelfAsync`, disconnect.
- Save channels: maps each `ChannelRole` to a channel id; all eight roles are required.
- Page: certificate status and buttons, "Verbindung prüfen", channel tree with a role dropdown per role, "Bot registrieren", hints for the ACL.
- All endpoints need global `options_edit`.
- [ ] **Step 1: Tests:** generator `Certificate_loads_with_password_and_has_private_key`; endpoints `Requires_options_edit`, `Generate_refuses_overwrite_without_replace`, `Save_requires_all_roles`, `Check_reports_unlinked_speaker_channel` (with `FakeMumbleServer`).
- [ ] **Step 2:** Implement, build, run both classes. Expected: PASS.

### Checkpoint 1

- [ ] Build, run all phase 1 classes, restart the local server.
- [ ] Stop. Hand over to the user: generate the certificate on the dev instance, set up the bot's ACL and the test channel tree on the Bundesmumble, then "Verbindung prüfen" and "Bot registrieren" together. Wait for the go.

---

# Phase 2: Queues, Console, Speaking Timer

### Task 6: Session state: queues, floor, timer (pure)

**Files:**
- Create: `Session/AssemblySessionState.cs`, `SpeakerQueue.cs`, `QueueEntry.cs`, `GraceStore.cs`, `SpeakingTimer.cs`, `AssemblyEffects.cs`, `AssemblyTimes.cs`
- Test: `AssemblySessionStateQueueTests.cs`, `AssemblySessionStateTimerTests.cs`

**Interfaces:**

```csharp
public record MemberIdentity(int? UserId, string? CertificateHash, string Name) {
    public string Key { get; }          // "u:<id>" | "h:<hash>" | "n:<name>"
}

public record AssemblyEffects(IReadOnlyList<(uint Session, uint ChannelId)> Moves, IReadOnlyList<(uint Session, string Text)> Texts,
    IReadOnlyList<AssemblyLogEntry> Log, bool Changed);

public class AssemblySessionState {
    public AssemblySessionState(Guid sessionId, IReadOnlyDictionary<ChannelRole, uint> channels, AssemblyTimes times, AssemblyMessages messages)
    public long Version { get; }

    // Mumble input
    public AssemblyEffects OnSynced(MumbleServerSnapshot snapshot, DateTime nowUtc)
    public AssemblyEffects OnUserEntered(MumbleUser user, uint? previousChannelId, DateTime nowUtc)
    public AssemblyEffects OnUserDisconnected(MumbleUser user, DateTime nowUtc)
    public AssemblyEffects OnSpeaking(uint session, bool started, DateTime nowUtc)
    public AssemblyEffects OnMoveFailed(uint session, MumbleMoveResult result, DateTime nowUtc)
    public AssemblyEffects Tick(DateTime nowUtc)

    // Chair commands; every command takes the version it was issued against and returns Rejected if it is stale
    public (AssemblyCommandResult Result, AssemblyEffects Effects) GiveFloor(long version, string entryKey, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) GiveFloorToUser(long version, uint session, ChannelRole target, Guid actor, DateTime nowUtc)  // Speaker or MotionAuthor
    public (AssemblyCommandResult, AssemblyEffects) Done(long version, uint session, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) AddMinute(long version, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) SetSpeechTime(long version, int seconds, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) SetMotionSpeechMinutes(long version, int? minutes, Guid actor, DateTime nowUtc)   // GO § 15h, null clears
    public (AssemblyCommandResult, AssemblyEffects) PauseTimer(long version, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) ResumeTimer(long version, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) StartTimerNow(long version, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) Skip(long version, string entryKey, bool skipped, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) Dismiss(long version, string entryKey, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) UndoDismiss(long version, string entryKey, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) CloseList(long version, DateTime? atUtc, Guid actor, DateTime nowUtc)   // null = now
    public (AssemblyCommandResult, AssemblyEffects) ReopenList(long version, Guid actor, DateTime nowUtc)
    public (AssemblyCommandResult, AssemblyEffects) SetItem(long version, string title, int? openSlidesAgendaItemId, AssemblyItemKind kind, Guid actor, DateTime nowUtc)

    public AssemblySnapshotDTO Snapshot(DateTime nowUtc)
    public AssemblyOverlayDTO OverlaySnapshot(DateTime nowUtc)
}
```

Rules (spec "Queues" and "Speaking timer"):
- Entry key = `MemberIdentity.Key` + queue kind. Entering a request channel appends; leaving it or disconnecting moves the entry into `GraceStore` with its position and the time; coming back into the same queue within `grace_seconds` restores the position (the session id is updated). Expired grace entries are dropped on `Tick`.
- Entering after the list was closed: flag `AfterListClosed`, never suggested. Phase 2 suggestion: the oldest non-skipped, non-dismissed, non-late entry of the highest queue in `QueueKind` order; phase 5 replaces it.
- Dismissed entries stay visible until the member leaves the channel; undo restores.
- `GiveFloor` produces exactly one move into the Speaker channel; the entry is consumed when the move is confirmed by `OnUserEntered` into Speaker. Until then the entry is `MovePending`; a second `GiveFloor` for it is rejected. `OnMoveFailed` clears the pending flag and puts a notice into the log.
- A user entering Speaker or MotionAuthor by any other means (manual move): if they have an entry, it is consumed with role from their queue; otherwise role "Rederecht (manuell)". Never moved back.
- `Done` moves the user to Listener; the timer is stopped and the speech is logged (`Speech` with `StartedAt`, `EndedAt`, `AllottedSeconds`, role).
- Timer: created when a user arrives in Speaker or MotionAuthor with the allotted time (per-speech override > per-motion override > default for the role; Question queue -> `question`). Starts at the first `OnSpeaking(started)` of that session, or by `StartTimerNow`; after `start_now_after_seconds` without speech the snapshot flags `OfferStartNow`. Warning text at 30 s remaining, expired text at 0, each once; overrun counts up. Pause/resume shift the end. Per-motion override is whole minutes and is cleared by `SetItem`.
- Every state change increments `Version`; effects are returned, never executed here.
- [ ] **Step 1: Tests** (fake clock = explicit `DateTime` values):
  - queues: `Entering_request_channel_appends_in_order`, `Leaving_removes_entry`, `Rejoin_within_grace_keeps_position`, `Rejoin_after_grace_appends`, `Rejoin_with_new_session_matches_by_hash`, `Registered_user_matches_by_user_id`, `Late_entry_after_close_is_flagged_and_not_suggested`, `Close_list_at_time_takes_effect_on_tick`, `Skip_and_unskip`, `Dismiss_and_undo`, `Give_floor_twice_moves_once`, `Stale_version_command_is_rejected`, `Move_failure_clears_pending`, `Manual_move_into_speaker_channel_consumes_request`, `Done_moves_to_listener_and_logs_speech`, `Give_floor_to_any_user_moves_into_mover_channel`.
  - timer: `Timer_starts_on_first_voice_packet`, `Offer_start_now_after_20_seconds`, `Warning_text_once_at_30_seconds`, `Expired_text_once_at_zero_and_overrun_counts`, `Pause_and_resume_shift_end`, `Add_minute_extends`, `Motion_override_applies_to_following_speeches_and_resets_on_item_change`, `Question_queue_uses_question_time`.
- [ ] **Step 2:** Implement, build, run both classes. Expected: PASS.

### Task 7: Session host, persistence, recovery

**Files:**
- Create: `Session/AssemblySessionHost.cs`, `Session/AssemblySessionRecovery.cs`, `Quartermaster.Data/AssemblyAssistant/AssemblySessionRepository.cs`
- Create: `Endpoints/AssemblySessionStartEndpoint.cs`, `AssemblySessionEndEndpoint.cs`
- Create: `Quartermaster.Server.Tests/AssemblyAssistant/Fakes/FakeMumbleConnection.cs`
- Modify: `Program.cs` (singleton `AssemblySessionHost`, also registered as hosted service so it resumes an open session on startup; it creates its connection through `IMumbleConnectionFactory` when a session starts)
- Test: `AssemblySessionHostTests.cs`, `AssemblySessionRepositoryTests.cs`

**Interfaces:**

```csharp
public class AssemblySessionHost : IHostedService {
    public bool IsRunning { get; }
    public Guid? EventId { get; }
    public Task<AssemblyCommandResult> StartAsync(Guid eventId, Guid userId, CancellationToken ct)    // refused if another session runs
    public Task<AssemblyCommandResult> EndAsync(Guid userId, CancellationToken ct)
    public AssemblySnapshotDTO? Snapshot()
    public AssemblyOverlayDTO? OverlaySnapshot(Guid eventId)
    public Task<AssemblyCommandResult> ExecuteAsync(Func<AssemblySessionState, DateTime, (AssemblyCommandResult, AssemblyEffects)> command)
}

public class AssemblySessionRepository {
    public AssemblySession Create(Guid eventId, int? meetingId, Guid userId, DateTime nowUtc)
    public AssemblySession? GetOpen()
    public void End(Guid sessionId, DateTime nowUtc)
    public void AddLog(IEnumerable<AssemblyLogEntry> entries)
    public AssemblyLogEntry? GetOpenSpeech(Guid sessionId)             // Kind == Speech, EndedAt == null, newest
    public List<AssemblyLogEntry> GetLog(Guid sessionId)
    public void DeleteLog(Guid sessionId, DateTime nowUtc)             // phase 7
}
```

Behaviour:
- One `System.Threading.Lock` guards the state. Mumble events and commands run the pure method under the lock, then execute the effects outside it: moves through `IMumbleConnection.MoveAsync` (result back into `OnMoveFailed` on failure), texts through `QueueText`, log entries through a scope's `AssemblySessionRepository`, and a snapshot publish when `Changed`.
- Tick: `PeriodicTimer` 250 ms; publishes only when `Changed`. The snapshot carries `ServerNowUtc` and timer end times so clients count down locally.
- Start: reads the event's portal for `OpenSlidesMeetingId` (may be null in phase 2), creates the session row, logs `SessionStarted`, starts the connection with the Chair channel as home. End: logs `SessionEnded`, stops the connection, clears the state.
- On application start: if `GetOpen()` returns a session, resume it. `AssemblySessionRecovery` rebuilds queues from the first `MumbleSynced` in channel order with `OrderUncertain = true`, treats the user in Speaker as the current speaker and restores the timer from `GetOpenSpeech` (start time and allotted seconds). The same reconciliation runs after every reconnect, keeping known entries in place.
- Endpoints: `POST /api/assembly/{eventId}/session/start` and `/end`, permission `assembly_chair` on the event's chapter.
- [ ] **Step 1: Tests:** `Start_refused_while_other_session_runs`, `Effects_are_executed_outside_lock` (fake connection records calls; a command from inside a fake event handler must not deadlock), `Move_failure_flows_back_into_state`, `Reconnect_reconciles_queues_and_marks_unknown_uncertain`, `Recovery_resumes_timer_from_log`, `Open_session_resumes_on_start`, repository round trips.
- [ ] **Step 2:** Implement, build, run both classes. Expected: PASS.

### Task 8: Assembly hub

**Files:**
- Create: `AssemblyHub.cs`, `Quartermaster.Api/AssemblyAssistant/AssemblyHubMethods.cs`, `AssemblySnapshotDTO.cs`, `AssemblyOverlayDTO.cs`, `AssemblyCommandResult.cs`
- Modify: `Program.cs` (`ep.MapHub<AssemblyHub>("/hubs/assembly")`)
- Test: `Integration/AssemblyAssistant/AssemblyHubTests.cs`

Behaviour:
- `JoinConsole(eventId)`: requires `assembly_view` on the event's chapter; adds to group `assembly:console:<eventId>` and returns the current snapshot plus `CanControl` (`assembly_chair`).
- `JoinOverlay(eventId)`: anonymous; group `assembly:overlay:<eventId>`; returns `AssemblyOverlayDTO` only.
- Command methods (one per state command in Task 6, all with `long version` first) require `assembly_chair`, run through `AssemblySessionHost.ExecuteAsync` and return `AssemblyCommandResult { Accepted, MessageKey }`.
- The host publishes `AssemblySnapshotChanged` to the console group and `AssemblyOverlayChanged` to the overlay group via `IHubContext<AssemblyHub>`.
- `AssemblyOverlayDTO` contains: item title and motion code, speaker Mumble name, role label, timer end/overrun/paused, next two names per Pro and Contra (phase 5), poll banner (phase 4), election marker. No user ids, no sessions, no queue keys.
- [ ] **Step 1: Tests:** `Console_join_requires_view_permission`, `Overlay_join_is_anonymous_and_gets_overlay_dto_only`, `Command_requires_chair_permission`, `Stale_version_command_is_rejected` (end to end through the hub with `FakeMumbleConnection`), `Snapshot_is_pushed_after_command`.
- [ ] **Step 2:** Implement, build, run `AssemblyHubTests`. Expected: PASS.

### Task 9: Console page, docs, changelog

**Files:**
- Create: `Quartermaster.Blazor/Services/AssemblyHubClient.cs`, `Pages/AssemblyAssistant/AssemblyConsole.razor(.cs)`, `Components/AssemblyAssistant/QueueColumn`, `SpeakerPanel`, `LogPanel`, `UserPicker` (each `.razor` + `.razor.cs`)
- Modify: `EventDetail.razor(.cs)` (button "Versammlungsassistent" for `assembly_view` holders), i18n, `Translations.md`, `Quartermaster.Documentation/Overview.md`, `Quartermaster.Server/Changelog/V5.md`
- Test: none beyond the build (UI); the checkpoint covers it.

Layout at `/assembly/{eventId}` (spec "Chair console", 1920 px wide):
- Header: item title (free text field + "Nächster TOP" in phase 2), phase, list status with close now / close at (Berlin time), Mumble connection state, session start/end.
- Centre: current speaker with role, timer (counts locally from `ServerNowUtc` offset), speaking dot, "Fertig", "+1 Min", "Pause/Weiter", "Jetzt starten" when offered, exact time and per-motion minutes inputs.
- Queues: GO-Antrag, Frage, Pro, Contra side by side; names, waiting time, flags (spät, unsicher, übersprungen, abgewiesen); buttons "Rederecht", "Überspringen", "Abweisen", "Rückgängig"; suggested entry highlighted.
- "Rederecht an ..." picker: search over all online users, target Redner or Antragsteller.
- Log panel: last 20 chair actions and notices (move failed, bot moved back, connection lost).
- Read-only mode when `CanControl` is false (same page, buttons hidden).
- Keyboard: `Space` give the floor to the suggestion, `F` done, `+` add a minute; ignored while an input has focus.
- A rejected command shows a toast and the page re-renders from the pushed snapshot.
- [ ] **Step 1:** Implement, build.

### Checkpoint 2

- [ ] Build, run all phase 2 classes, restart the local server.
- [ ] Stop. Hand over: the user tests with real clients in the test channel tree on the Bundesmumble. Wait for the go.

---

# Phase 3: Overlay

### Task 10: Overlay page and fonts

**Files:**
- Create: `Layout/OverlayLayout.razor(.cs)`, `Pages/AssemblyAssistant/AssemblyOverlay.razor(.cs)`, `wwwroot/css/overlay.css`
- Create: `wwwroot/fonts/PoliticsHead.otf`, `DejaRip.otf`, `DejaRip-Bold.otf` (from `PoliticsHeadv1_9.zip` and `DejaRip1.2_otf.zip`, Piratenwiki page "Pirate Design/Schriften"; fetch with `api.php`-style tool User-Agent, see Configuration.md for the wiki)
- Modify: `index.html` only if the boot screen paints a background that cannot be overridden from the layout
- Test: `Integration/AssemblyAssistant/AssemblyOverlayTests.cs` (the overlay DTO through the hub, already partly in Task 8: `Overlay_dto_has_no_identifiers`, `Overlay_updates_on_speaker_change`)

Behaviour:
- `/assembly/{eventId}/overlay`, no login, `OverlayLayout` without nav and toaster; `html` and `body` transparent (`background: transparent !important`), fixed 1920x1080 canvas.
- Lower third: name in DejaRip Bold, role badge in PoliticsHead, remaining-time bar (normal, orange from 30 s, red at 0 with overrun "+0:12").
- Small header with item and motion code. Optional side column (query `?next=1`) with the next two names per side. Poll banner and election marker slots (filled in phase 4 and 6).
- Reconnects on its own (`WithAutomaticReconnect` plus retry loop after `Closed`), shows nothing while disconnected.
- [ ] **Step 1:** Implement, build, run `AssemblyOverlayTests`.

### Checkpoint 3

- [ ] Build, restart. Stop. Hand over: the user adds the overlay as an OBS browser source in the scene "Versammlung". Wait for the go.

---

# Phase 4: OpenSlides Live

### Task 11: Client extensions and session tokens

**Files:**
- Modify: `IOpenSlidesClient.cs`, `HttpOpenSlidesClient.cs`, `Quartermaster.Server.Tests/AssemblyMotions/Fakes/FakeOpenSlidesClient.cs`
- Create: `OpenSlides/OpenSlidesPollCreate.cs`
- Test: extend `HttpOpenSlidesClientTests.cs`

**Interfaces** (added to `IOpenSlidesClient`):

```csharp
Task<OpenSlidesSessionTokens> GetSessionTokensAsync(OpenSlidesSettings settings, CancellationToken ct);   // logs in if needed
void ReportRefreshedToken(string authentication);                                                         // from the autoupdate response
Task<int> CreatePollAsync(OpenSlidesSettings settings, OpenSlidesPollCreate poll, CancellationToken ct);
Task StartPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);
Task StopPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);
Task PublishPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);
Task ResetPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);
Task DeletePollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);
Task UpdateCountdownAsync(OpenSlidesSettings settings, int countdownId, bool running, double countdownTime, CancellationToken ct);
Task<int> CreateTopicAsync(OpenSlidesSettings settings, int meetingId, string title, int agendaParentId, CancellationToken ct);   // phase 6
Task ProjectAsync(OpenSlidesSettings settings, int meetingId, IReadOnlyList<int> projectorIds, string contentObjectId, string? type, CancellationToken ct);   // phase 7

public record OpenSlidesSessionTokens(string AccessToken, string? RefreshId);
public record OpenSlidesPollOption(string? Text, string? ContentObjectId);
public record OpenSlidesPollCreate(int MeetingId, string Title, string ContentObjectId, string PollMethod, IReadOnlyList<OpenSlidesPollOption> Options,
    int EntitledGroupId, int MinVotes, int MaxVotes, int MaxVotesPerOption, bool GlobalYes, bool GlobalNo, bool GlobalAbstain, string OnehundredPercentBase);
```

- `poll.create` body: `meeting_id`, `title`, `type = "pseudoanonymous"` (fixed, not a parameter), `pollmethod`, `content_object_id`, `options` (`{"text"}` or `{"content_object_id"}`), `entitled_group_ids`, `min_votes_amount`, `max_votes_amount`, `max_votes_per_option`, `global_yes`, `global_no`, `global_abstain`, `onehundred_percent_base`, `backend = "fast"`.
- `poll.start|stop|publish|reset|delete`: `{"id"}`. `projector_countdown.update`: `{"id", "running", "countdown_time"}`. `topic.create`: `meeting_id`, `title`, `agenda_parent_id`, `agenda_type = "common"`.
- All go through the existing gate, login and 401 handling.
- [ ] **Step 1: Tests:** `Poll_create_body_is_pseudoanonymous_with_entitled_group`, `Poll_create_reads_id`, `Poll_lifecycle_actions_send_id`, `Countdown_update_body`, `Session_tokens_log_in_once`, `Reported_token_replaces_stored_token`.
- [ ] **Step 2:** Implement, build, run `HttpOpenSlidesClientTests`. Expected: PASS.

### Task 12: Autoupdate stream and meeting view

**Files:**
- Create: `OpenSlides/OpenSlidesSubscription.cs`, `OpenSlidesKeyStore.cs`, `OpenSlidesMeetingView.cs`, `OpenSlidesAutoupdateClient.cs`
- Modify: `Program.cs` (named `HttpClient` `"openslides-stream"`: no redirects, `UseCookies = false`, `Timeout = Infinite`)
- Test: `OpenSlidesSubscriptionTests.cs`, `OpenSlidesKeyStoreTests.cs`, `OpenSlidesMeetingViewTests.cs`, `OpenSlidesAutoupdateClientTests.cs`

**Interfaces:**

```csharp
public static class OpenSlidesSubscription {
    public static string Build(int meetingId)      // JSON body, spec "Subscription"
}

public class OpenSlidesKeyStore {
    public bool Apply(JsonElement line)            // full map or delta; null removes; {} heartbeat returns false
    public JsonElement? Get(string key)
}

public record OpenSlidesPollOptionView(int Id, string? Text, string? ContentObjectId, decimal Yes, decimal No, decimal Abstain);
public record OpenSlidesPollView(int Id, string Title, string State, string Type, string PollMethod, string ContentObjectId,
    decimal? VotesValid, decimal? VotesCast, int LiveVoteCount, IReadOnlyList<OpenSlidesPollOptionView> Options, int? GlobalOptionId);
public record OpenSlidesAgendaItemView(int Id, string? ItemNumber, int? ParentId, int Weight, string ContentObjectId, string Title, string? MotionNumber);
public record OpenSlidesCountdownView(int Id, bool Running, double CountdownTime, int DefaultTime);
public record OpenSlidesMeetingView(IReadOnlyList<OpenSlidesPollView> Polls, IReadOnlyList<OpenSlidesAgendaItemView> AgendaItems, OpenSlidesCountdownView? PollCountdown) {
    public static OpenSlidesMeetingView From(OpenSlidesKeyStore store, int meetingId)
}

public class OpenSlidesAutoupdateClient {
    public event Action<OpenSlidesMeetingView>? Changed;
    public event Action<bool, string?>? ConnectionChanged;
    public Task StartAsync(OpenSlidesSettings settings, int meetingId, CancellationToken ct)   // keeps reconnecting until StopAsync
    public Task StopAsync()
}
```

- Stream: `POST {Url}/system/autoupdate`, headers `Authentication` and `Cookie: refreshId=...` from `GetSessionTokensAsync`, `HttpCompletionOption.ResponseHeadersRead`, read line by line; an `Authentication` response header goes to `ReportRefreshedToken`. 401 -> `LoginAsync` and retry. Drop or no line for 6 minutes -> reconnect with backoff 1 to 30 s. Each reconnect starts a fresh key store (the first line is the full map).
- `LiveVoteCount` = number of keys in `poll/<id>/live_votes` (an object), 0 if absent.
- [ ] **Step 1: Tests:** subscription JSON snapshot test; key store `Full_map_then_delta`, `Null_removes_key`, `Heartbeat_changes_nothing`; meeting view built from a recorded key map (hand-written fixture with two polls, agenda items and the countdown): `Live_vote_count_counts_keys`, `Agenda_item_has_motion_number`; client with a handler that streams lines: `Stream_applies_lines_in_order`, `Drop_reconnects_with_new_tokens`, `Unauthorized_logs_in_again`.
- [ ] **Step 2:** Implement, build, run the four classes. Expected: PASS.

### Task 13: Poll evaluation

**Files:**
- Create: `OpenSlides/PollEvaluation.cs`
- Test: `PollEvaluationTests.cs`

**Interfaces:**

```csharp
public record PollEvaluationResult(bool? Passed, string SummaryKey, IReadOnlyDictionary<string, string> Parameters, int? WinnerOptionId, bool RunoffNeeded);

public static class PollEvaluation {
    public static PollEvaluationResult Evaluate(OpenSlidesPollView poll, PollEvaluationKind kind, int threshold)
}
```

- Simple: Ja > Nein (Enthaltungen ignored). TwoThirds: Ja >= 2 * Nein and Ja > 0. Threshold: Ja >= N. NumbersOnly: `Passed = null`. Acceptance (GO § 7): highest Ja wins if its Ja > half of `VotesValid` (ballots incl. the global "keinen der Anträge"); a tie for first place or no option above half -> `RunoffNeeded`.
- For Ja/Nein topic polls (two text options) "Ja" and "Nein" are read from the option texts; for motion polls from `Yes/No/Abstain` of the single option.
- [ ] **Step 1: Tests:** `Simple_yes_greater_no`, `Simple_tie_fails`, `Two_thirds_with_abstentions_34_17_passes`, `Two_thirds_33_17_fails`, `Threshold_20_reached`, `Topic_poll_reads_ja_nein_options`, `Acceptance_winner_above_half`, `Acceptance_below_half_needs_runoff`, `Acceptance_tie_needs_runoff`.
- [ ] **Step 2:** Implement, build, run `PollEvaluationTests`. Expected: PASS.

### Task 14: Poll automation, agenda, console and overlay poll panel

**Files:**
- Create: `OpenSlides/PollAutomation.cs`, `Components/AssemblyAssistant/PollPanel.razor(.cs)`
- Modify: `AssemblySessionState.cs` (OpenSlides view, poll settings, item picker from the agenda), `AssemblySessionHost.cs` (starts/stops `OpenSlidesAutoupdateClient` with the session when the portal has a meeting id; executes OpenSlides effects), `AssemblyHub.cs` (`SetPollSettings`, `ExtendPoll`, `StopPoll`, `PublishPoll`, `SelectAgendaItem`), console, overlay, `V5.md`, `Configuration.md` (rights of the OpenSlides account: group with `poll.can_manage`, `motion.can_manage_polls`, `agenda_item.can_manage`, optional `projector.can_manage`; `assembly.openslides.delegates_group_id`)
- Test: `PollAutomationTests.cs`

**Interfaces:**

```csharp
public record PollSettings(int DurationSeconds, bool AutoStart, bool AutoStop, bool AutoPublish);   // session copy, changed live in the console

public abstract record OpenSlidesEffect;
public record StopPollEffect(int PollId) : OpenSlidesEffect;
public record PublishPollEffect(int PollId) : OpenSlidesEffect;
public record SetCountdownEffect(int CountdownId, bool Running, double CountdownTime) : OpenSlidesEffect;

public class PollAutomation {
    public IReadOnlyList<OpenSlidesEffect> OnView(OpenSlidesMeetingView view, PollSettings settings, DateTime nowUtc)
    public IReadOnlyList<OpenSlidesEffect> Tick(OpenSlidesMeetingView view, PollSettings settings, DateTime nowUtc)
    public IReadOnlyList<OpenSlidesEffect> Extend(OpenSlidesMeetingView view, int seconds)
}
```

- A poll seen switching to `started`: if the countdown's `default_time` differs from `DurationSeconds`, emit `SetCountdownEffect(running: true, countdown_time: now + duration)`. Applies to polls created by hand too.
- Tick: countdown running and `countdown_time <= now` and poll `started` and `AutoStop` -> one `StopPollEffect` per poll id (remembered until the poll leaves `started`). Poll seen `finished` and `AutoPublish` -> one `PublishPollEffect`.
- Evaluation kind per poll: motion poll -> type's `RequiredMajority` via `AssemblyMotion.OpenSlidesMotionId` (Simple or TwoThirds); topic polls -> NumbersOnly unless created from a template (phase 6 remembers the kind per poll id in memory).
- Agenda picker in the console replaces the free text item; `SelectAgendaItem` sets title, motion code and kind (motion if content object is `motion/...`).
- Overlay banner: "Abstimmung läuft", title, countdown (from `countdown_time`), live count; then results with evaluation for 15 s after publish.
- OpenSlides unreachable: poll panel disabled with notice, everything else keeps running.
- [ ] **Step 1: Tests:** `Different_duration_sets_countdown_after_start`, `Auto_stop_sends_stop_once`, `Auto_stop_off_sends_nothing`, `Auto_publish_after_finished_once`, `Extend_moves_countdown_time`, `Manual_poll_gets_same_handling`, `Motion_poll_uses_type_majority`.
- [ ] **Step 2:** Implement, build, run `PollAutomationTests` and `AssemblyHubTests`. Expected: PASS.

### Checkpoint 4

- [ ] Build, run all phase 4 classes, restart. Stop. Hand over: the user tests against a test meeting in OpenSlides (the first real OpenSlides contact is the user's). Wait for the go.

---

# Phase 5: Phases and Suggestions

### Task 15: Flow and suggestion engine

**Files:**
- Create: `Flow/AssemblyFlow.cs`, `Flow/SuggestionEngine.cs`, `Flow/GoMotionInterruption.cs`
- Modify: `AssemblySessionState.cs` (phase, round counters, flow commands), `AssemblyHub.cs` (`SetPhase`, `StartNextRound`, `RecordGoAccepted`, `StartGoObjection`, `ResumeAfterGo`)
- Test: `SuggestionEngineTests.cs`, `AssemblyFlowTests.cs`

Rules (spec "Phases and suggestions"):
- Motion flow: Presentation (motion author via picker, presentation time) -> Questions (Frage queue only, question time; motion author answers untimed) -> Debate round n (alternate Pro/Contra while both have entries, starting with Pro; 5 per side per round; unused slots do not pass over) -> RoundEnd ("Durchgang beendet", waiting count, poll offer "Debatte fortsetzen?" in phase 6) -> Statement (motion author, 1 min) -> next round -> ClosingWord (3 min) -> Vote.
- Question and GO entries are suggested before Pro/Contra, but only when no speech is running and no poll is `started`.
- General debate flow: one combined queue over all request channels in order of entry, speech time, list closing at a set time.
- GO interruption: speaking GO member (untimed) -> "Gegenrede?" -> either "ohne Gegenrede angenommen" (logged) or one objection from any queue and the GO poll offer; afterwards the interrupted phase and counters resume.
- Election flow (phase 5 only the timing part): CandidatePresentation (5 min, repeat 3 min), CandidateQuestions (max 5 questioners, 30 s question / 2 min answer), ElectionRunning marker on the overlay.
- Overlay side column gets the next two suggestions per side.
- [ ] **Step 1: Tests:** `Alternates_pro_contra_starting_with_pro`, `Only_one_side_left_continues_that_side_until_its_five`, `Unused_slots_do_not_pass_over`, `Round_end_when_both_full`, `Round_end_when_both_empty`, `Question_suggested_before_pro`, `Go_not_suggested_during_speech_or_poll`, `Go_interruption_resumes_counters`, `General_debate_single_queue_in_entry_order`, `Questions_phase_suggests_only_questions`, `Candidate_questions_limited_to_five`, `Statement_after_continue_then_next_round`.
- [ ] **Step 2:** Implement, build, run both classes. Expected: PASS.

### Task 16: Console for flows, docs, changelog

- [ ] Phase header with phase switcher, round counters "Pro 3/5, Contra 2/5", round-end banner, GO dialog, election timing; `V5.md`, `Overview.md`. Build.

### Checkpoint 5 (dry run readiness)

- [ ] Build, run all phase 5 classes, restart. Stop. Hand over for the dry run around 21.11. Wait for the go.

---

# Phase 6 (Could): Poll Templates, Procedural Topics, Elections

### Task 17: Procedural topics and poll templates

**Files:**
- Create: `Templates/ProceduralTopics.cs`, `Templates/AssemblyPollTemplates.cs`
- Modify: `AssemblySessionState.cs`, `AssemblyHub.cs` (`CreatePollFromTemplate`, `CreateProceduralTopics`, `ResetPoll`, `DeletePoll` with confirmation flag), `PollPanel`
- Test: `AssemblyPollTemplatesTests.cs`, `ProceduralTopicsTests.cs`

- Procedural topic "<Code>: Verfahren" as child of the motion's agenda item; found again by title among the agenda children (no persistence); created on first use or for all motions at once.
- Templates (spec table): Debatte fortsetzen?, GO-Antrag: <text>, Meinungsbild: <question>, Unterstützung <name/motion> (N 10 or 20), Kontroverse <motion> (N 10), Konkurrenz <A> / <B> (acceptance, from the portal's `AssemblyMotionCompetition` rows), Schlussabstimmung (motion poll YNA), Blockabstimmung.
  - Ja/Nein topic polls: options "Ja", "Nein", `pollmethod = "Y"`, min 1, max 1, per option 1, no global options.
  - Acceptance: one option per motion `<code> <title>`, `pollmethod = "Y"`, min 0, max = option count, per option 1, `global_no = true` ("keinen der Anträge"), evaluation Acceptance; runoff template with one vote and the global no.
  - Final vote: `content_object_id = motion/<id>`, option `{"content_object_id": "motion/<id>"}`, `pollmethod = "YNA"`.
  - Titles carry the motion code. Auto start per session setting.
- Reset and delete only for polls this session created.
- [ ] **Step 1: Tests:** one body test per template, `Procedural_topic_reused_by_title`, `Acceptance_options_from_competition`, `Reset_refused_for_foreign_poll`.
- [ ] **Step 2:** Implement, build, run both classes. Expected: PASS.

### Task 18: Election flow

- [ ] Candidate list (entered by the chair, sorted by surname), support polls per candidate (N = 20), presentation order, questions round poll, SecretBallot marker; tests `ElectionFlowTests` (`Candidates_sorted_by_surname`, `Support_reached_at_20`); build, run.

### Checkpoint 6

- [ ] Build, run phase 6 classes, restart. Stop. Wait for the go.

---

# Phase 7 (Could): Minutes Help and Projector

### Task 19: Log export, read-only view, deletion, projector

**Files:**
- Create: `Export/AssemblyLogExport.cs`, `Endpoints/AssemblyLogExportEndpoint.cs`, `AssemblyLogDeleteEndpoint.cs`
- Test: `AssemblyLogExportTests.cs`, `Integration/AssemblyAssistant/AssemblyLogEndpointTests.cs`

- CSV (`;`, UTF-8 with BOM) and Markdown (one section per agenda item: speeches with role and duration, polls with result and evaluation, chair actions), times in Berlin time; `assembly_view`.
- Delete: `assembly_chair`, only for ended sessions, sets `LogDeletedAt` and removes the entries.
- `assembly_minutes` role sees the console read-only (already from Task 9) plus export buttons.
- Projector (optional switch in the poll panel): project the running poll and the countdown on the configured projector ids.
- [ ] **Step 1:** Tests `Markdown_groups_by_item`, `Csv_has_bom_and_berlin_times`, `Delete_refused_for_running_session`, `Export_requires_view_permission`; implement, build, run.

### Checkpoint 7 (final)

- [ ] Build, run phase 7 classes, restart, final `V5.md` and docs pass. Stop and hand over.
