# Assembly Assistant (Versammlungsassistent): Design

Status: design 2026-10-16, for the online Bundesparteitag 2026.2 (planned Saturday 28.11.2026). Built as a Quartermaster module and shipped as V5 with its own migration (V4, the motion portal, goes live before).

## Goal

Take the mechanical work of chairing an online assembly off the chair, so that the chair only decides. Speaker queues, speaking times, the stream overlay and recurring OpenSlides polls run automatically. People in Mumble are never moved automatically: every move happens on an explicit click by the chair or a chair helper. Without the tool, manual operation stays possible at any moment.

## Background

At the BPT 2026.1 (10.10.2026) one person chaired alone and had to lead the debate, watch the Mumble request channels, move people, take speaking times, create polls in OpenSlides, drive the projector and run the stream at the same time. That does not work. The lessons are recorded in PiratenWissen ("Onlineparteitag vorbereiten und leiten") and in `TODO/naechster-obpt.md` (section "Werkzeuge").

The setup that stays in place:

- OpenSlides 4 (self-hosted, SAML login via Keycloak) holds agenda, motions and all open polls. Members vote there and must mark themselves present. Only the group `delegates` may vote; that group is selected per poll.
- The Bundesmumble carries all speech. Channel tree "Bundesparteitag": internal chair room, chair room, speaker room ("Redner"), request rooms ("Wortmeldung / Pro", "Contra", "GO-Antrag"), listener room ("Zuhörer"), all linked except the internal room. Added before 28.11. and required: request room "Frage" and room "Antragsteller" (linked and permissioned like "Redner"). Members enter a request room themselves; they get into "Redner" only by being moved.
- The stream runs from OBS to Twitch. OBS can show a web page as a browser source.
- Secret elections run in SecretBallot, which is out of scope here.

## Rules the tool implements

From the rules of procedure (GO BPT, version adopted 10.10.2026) and the statutes. The tool suggests and displays; the chair decides and announces (GO § 6 Abs. 7).

```
Rule                                   Source            Tool behaviour
Presentation by the motion author      GO § 12 (1)       phase "Vorstellung", default 5 min (configurable)
Questions any time, preferred          GO § 12 (3),      queue "Frage" ranks above Pro/Contra;
                                       § 13 (1)          default 30 s per question
Debate: 5 Pro + 5 Contra per round,    GO § 13 (2)       per-round counters, alternation suggestion
alternating while both have speakers
Default speaking time 3 min            GO § 13 (4)       timer default
Continue debate? only a clear          GO § 13 (3)       round-end notice + poll template
majority ends it
1 min statement of the motion author   GO § 13 (5)       phase "Stellungnahme" when a new round starts
after each extension
Closing word 3 min                     GO § 12 (5)       phase "Schlusswort"
Speaking time change applies until     GO § 15h          per-motion override, reset on next motion
the end of the current motion
Chair may close the list of speakers   GO § 12 (4)       "close list" now or at a set time
GO motions first, never interrupt a    GO § 15 (4)       queue "GO-Antrag" ranks highest, but is only
speech or a running poll                                 suggested after the current speech / poll
Candidates: 5 min (3 min again),       GO § 11           election phases with these defaults
up to 5 questioners, 30 s / 2 min
Support = enough Yes votes             GO § 9 (2)        support poll template, "reached" if Yes >= N
(20 for board candidates)              GO § 10 (1)
Controversy in block vote: 10          GO § 15n          per-motion support poll with N = 10
Simple majority: Yes > No;             Satzung § 9b (9), evaluation shown with every result;
abstentions are invalid                GO § 6 (2a)       100 % base = Yes + No
Statute and programme: 2/3             Satzung § 12 (1)  Yes >= 2 * No
```

## Terms

```
German (UI)                  English (code)        Meaning
Versammlungsleitung (VL)     chair                 elected chair; holds the chair permission
VL-Helfende                  chair helper          same permission, appointed by the chair (GO § 3 Abs. 2)
Wortmeldung                  request               a member waiting in a request channel
Wortmeldekanal               request channel       Pro, Contra, Frage, GO-Antrag (and "Wortmeldung" in a general debate)
Redner                       speaker channel       where the current speaker sits
Antragsteller                motion author channel where the motion author or candidate sits during the item
Rederecht                    give the floor        move a queued member into the speaker channel
Fertig                       done                  move the speaker back to the listener channel
Durchgang                    round                 up to 5 Pro + 5 Contra speeches
Redezeit                     speaking time         per-speech countdown
Verfahrensabstimmung         procedural poll       a poll that is not the vote on the motion itself
Verfahrens-TOP               procedural topic      plain OpenSlides topic below a motion's agenda item, carrying its procedural polls
Einblendung                  overlay               web page used as an OBS browser source
```

## Scope

In scope:

- A Mumble bot that connects as an ordinary client, tracks channel occupancy and who is speaking, moves users on command and sends text messages.
- Speaker queues per request channel in order of entry, with a grace period for short disconnects.
- A web console for the chair and chair helpers: queues with waiting times, suggestion for the next speaker, "give the floor" and "done" buttons, phase and round counters, speaking timer, list closing.
- A speaking timer that starts with the speaker's first voice packet, warns via Mumble text message and shows expiry clearly. It never moves anybody.
- An overlay page for OBS: current speaker with role, remaining time, next names, running poll with countdown and live count, result with majority evaluation.
- OpenSlides integration: live poll status via the autoupdate service, standard polls on one click (create and start), automatic stop when the poll timer runs out, result display with evaluation.
- A session log (speeches, polls, chair actions with timestamps) as a help for the minutes.

Not in scope:

- Automatic moves in Mumble, ever.
- Recording, decoding or transcribing audio. Voice packets are only inspected for the sender.
- Secret ballots (SecretBallot) and anything that changes how members vote.
- Video, subtitles, OBS scene switching (possible later through obs-websocket; noted under "Later").
- Replacing OpenSlides. Everything the tool does in OpenSlides can still be done by hand there at the same time.

## Hosting

Quartermaster module. It reuses the Keycloak login and chapter-scoped permissions, the OpenSlides login and action client from the motion portal (`Quartermaster.Server/AssemblyMotions/OpenSlides/`), DB-backed options with masked secrets and the settings page, hosted services, SignalR (pattern: `MeetingHub`, which already allows anonymous read-only connections), the audit log and the one systemd deployment. The motion portal already knows the event, its OpenSlides meeting id, every motion's `OpenSlidesMotionId` and its type with `RequiredMajority`, so the required majority (2/3 for GP, WP and SÄA) is derived without typing.

Costs: the first long-lived socket client in Quartermaster, and a restart of Quartermaster on assembly day also restarts the bot (state recovery below). No deployment on assembly day. Quartermaster is single-process, which suits the in-memory session state; one assembly session runs at a time. The Mumble connection is written against an interface (`IMumbleConnection`), so it could still be moved into its own process later without touching the session logic.

## Architecture

```
                      +-------------------- Quartermaster.Server -------------------+
Bundesmumble  <-TLS-> | MumbleConnection (hosted service)                            |
                      |   channel/user model, voice sender detection, move, text     |
                      |            | events                  ^ commands              |
                      |            v                         |                       |
                      | AssemblySession (singleton, in memory, Lock)                 |
                      |   phase, queues, rounds, current speaker, timers, polls      |
                      |            ^                         |                       |
OpenSlides   <-HTTPS->| OpenSlidesLive (hosted service)      | state snapshots       |
  autoupdate stream   |   poll/agenda subscription, actions  v                       |
  action service      | AssemblyHub (SignalR /hubs/assembly)                         |
                      +------------------------------|-------------------------------+
                                                     |
                     chair console (Blazor, permission)   overlay page (anonymous, read-only)
```

- `MumbleConnection` owns the socket. It is connected only while an assembly session runs: the chair starts the session in the console, and ending the session disconnects the bot. It raises events (user entered channel, user left, user disconnected, speaking started, speaking stopped, permission denied) and accepts commands (move user, send text). It knows nothing about assemblies.
- `AssemblySession` is the single source of truth for the running assembly. All mutations go through it under one `System.Threading.Lock`; every mutation increments a state version and publishes a snapshot to the hub. Timers run on one `PeriodicTimer` tick (250 ms) inside the session, not as separate timers per speech.
- `OpenSlidesLive` holds the autoupdate subscription and calls actions. It extends the existing OpenSlides client with poll, topic and (optional) projector actions. The client is a singleton with one access token and one `refreshId` cookie shared by the motion push, the actions and the autoupdate stream, so login, token refresh and cookie updates become safe for concurrent use.
- `AssemblyHub` pushes snapshots. Chair commands are hub methods guarded by the permission. Each command carries the state version it was issued against; if the state has moved on (two chairs clicked at the same time), the command is rejected and the console refreshes. This prevents double moves.

## Mumble integration

### Identity and rights

The Bundesmumble runs Murmur 1.3.4 (Ubuntu 22.04 package; version read by a UDP ping on 2026-10-16). The superadmin registers the bot and grants its rights.

The bot connects with its own long-lived self-signed client certificate under a fixed name (default "Versammlungsassistent") and registers itself once (`UserState{session=self, user_id=0}`, needs SelfRegister, which `all` has by default), so ACL groups can refer to it by user id. The certificate hash keeps it the same registered user across reconnects.

Moving another user needs Move in the user's current channel, and either Move in the destination or Enter for the moved user there (verified in the server source). Members have no Enter in "Redner" and "Antragsteller", so the bot needs Move on both sides. Move is not a default right: grant it to the bot (in `bpt-team` or a dedicated group) on "Bundesparteitag" with "apply to subchannels".

The bot sits in "Versammlungsleitung" (the linked chair room, not the internal one), self-muted but not self-deafened, so it cannot be dragged around by accident in the crowded "Zuhörer". A client receives voice from its own channel and from every channel reachable through links (1.3.4 follows links transitively, `Channel::allLinks`) in which the speaker has Speak; a deafened client receives nothing. Speakers in "Redner" and "Antragsteller" have Speak in "Versammlungsleitung" (that is how the chair hears them), so the bot receives the same voices as the chair, including the chair's own. Channel listening (`listening_channel_add`) does not exist in 1.3.4, so the bot's own channel decides what it hears. If the bot is moved out of its channel, it moves itself back right away and reports it in the console.

### Protocol

Verified against mumble-voip/mumble v1.5.939 (`src/Connection.cpp`, `src/MumbleProtocol.*`, `src/murmur/Server.cpp`, `src/murmur/Messages.cpp`) and re-checked against tag 1.3.4: move permission checks, `hash` in UserState, text message limit (`messagelimit` 1, `messageburst` 5), voice routing into linked channels with the Speak check, the TCP fallback for clients without UDP and the 30 s timeout are the same. The control channel is implemented directly; no .NET library is used (MumbleSharp was last changed in 2023, speaks protocol 1.2.0, has no `listening_channel_add` and is built around audio decoding).

- Transport: TLS over TCP, default port 64738, `SslStream` with the client certificate. Frames are a 2-byte big-endian message type, a 4-byte big-endian length and the protobuf body. `Mumble.proto` (proto2) is compiled with Google.Protobuf.
- Handshake: the server sends Version first; the bot sends Version with `version_v1 = 0x00010400` (1.4.0) and Authenticate with `opus = true` and `client_type = 1` (bot; 1.3.4 ignores the field). The server answers with CryptSetup, CodecVersion, all ChannelState messages, channel links, the bot's own UserState, all other UserStates, then ServerSync (= initial sync complete), ServerConfig. In the initial user list a missing `channel_id` means the root channel; later UserStates are deltas keyed by `session`.
- Keepalive: the server never pings and drops a connection that was silent for 30 s (checked every 15.5 s). The bot sends Ping every 10 s and treats four unanswered pings as a dead connection.
- Voice without UDP: the bot never sends UDP packets, so the server never registers a UDP address for it and tunnels every voice packet through TCP as `UDPTunnel` (type 1), unencrypted. 1.3.4 only knows the legacy format; a later 1.5 server would re-encode packets into it for a client that advertises 1.4.0.
- Legacy voice packet: header byte (top 3 bits codec, 4 = Opus; low 5 bits audio context: 0 normal, 1 shout, 2 whisper, 3 listen), then the speaker's session and the sequence number as Mumble varints (not LEB128: prefix-coded, 7/14/21/28/32/64 bits), then for Opus a varint `length | 0x2000` where `0x2000` marks the end of transmission. The bot reads header, session and that terminator bit and discards the rest.
- Moves: `UserState{session = target, channel_id = destination}`. Failures come back as PermissionDenied (type Permission or ChannelFull); a move into an unknown channel or the current channel is dropped silently, so the bot confirms every move by the following UserState broadcast and reports a timeout (3 s) to the console.
- Text messages: `TextMessage{session = [target]}` to one user. The server rate-limits text messages (default 1 per second, burst 5, excess dropped silently); warnings are therefore queued and never sent more than one per second.
- Identity of members for the grace period: the registered user id if the member is registered, otherwise the certificate hash from UserState if the server sends it, otherwise the name.

### Channel roles

Channels are mapped to roles in the settings by channel id (picked from a list the bot reads from the server, so renaming a channel does not break anything):

```
Role         Default channel         Purpose
chair        Versammlungsleitung     chair speaks from here; bot's own channel
speaker      Redner                  current speaker
motion_author Antragsteller          motion author or candidate of the current item
pro          Wortmeldung / Pro       request channel
contra       Contra                  request channel
question     Frage                   request channel (optional)
point        GO-Antrag               request channel for GO motions
listener     Zuhörer                 "done" moves people back here
```

A role without a mapped channel is shown as a configuration error in the console; "Frage" and "Antragsteller" are required.

### Speaking detection

A user counts as speaking from the first voice packet with that sender until a terminator packet arrives or no packet has arrived for 500 ms (configurable). Detection runs for every user whose voice reaches the bot, not only for the current speaker: the console shows a speaking indicator next to the chair and the speaker, and the session log records speech start and end.

## Queues

- A queue exists per request channel. A member is appended when they enter the channel and removed when they leave it, are moved out by the chair, or disconnect.
- Grace period: if a member leaves a request channel or disconnects and comes back into the same channel within 30 s (configurable), they get their old position back. Members are recognised by the certificate hash, otherwise by user name.
- Being moved into "Redner" consumes the request. "Done" moves the speaker to "Zuhörer", not back into the queue.
- Members who enter a request channel after the list was closed are shown as "nach Schluss der Redeliste" and are not suggested; the chair can still give them the floor.
- Every entry shows the waiting time. Entries can be skipped (stay in the queue, not suggested until un-skipped) or dismissed (removed from the queue without moving; the member stays in the channel and is shown as dismissed).
- "Give the floor" also works for any online user outside the queues: the console has a picker over all users on the server (search by name). This is how the motion author or a candidate gets into "Antragsteller", since Mumble names are free text and cannot be matched to submitters.
- Manual moves made by people in Mumble are recognised: if someone is moved into "Redner" by hand, the tool treats it as "give the floor" from the right queue. The tool never fights manual operation.

## Phases and suggestions

The chair picks the current agenda item in the console (list read from OpenSlides). The kind of item selects a flow; the chair can switch phase at any time.

Motion flow:

1. Vorstellung: the chair picks the motion author from the user picker and the bot moves them to "Antragsteller"; timer with the presentation default.
2. Fragen: suggestions come from "Frage" only; timer 30 s per question; the motion author answers from "Antragsteller" without a timer.
3. Aussprache, round n: suggestion alternates Pro and Contra while both queues have members, starting with Pro; counters show "Pro 3/5, Contra 2/5"; a side's unused slots do not pass to the other side. Questions and GO motions are suggested before Pro/Contra, but only after the current speech ends.
4. Round end: when both counters are full or both queues are empty, the console shows "Durchgang beendet" with the number of members still waiting, and offers the poll "Debatte fortsetzen?". If the debate continues, the next phase is "Stellungnahme" (motion author, 1 min), then round n+1.
5. Schlusswort: motion author, 3 min.
6. Abstimmung: the console offers the final vote with the majority derived from the motion type.

Election flow: Kandidaturen (support polls, 20 Yes), Vorstellung per candidate in alphabetical order of surname (5 min, 3 min for a repeated presentation), Befragung (up to 5 questioners, 30 s per question, 2 min per answer, then the poll whether another round of questions follows). The election itself runs in SecretBallot; the console only shows a marker "Wahlgang läuft (SecretBallot)" on the overlay when the chair sets it.

General debate flow (e.g. TOP "Aussprache"): one queue across all request channels in order of entry, default 3 min, optional closing of the list at a set time (for 2026.2: 11:30).

GO motion flow, inserted into any phase: the member from "GO-Antrag" speaks (no fixed time), then the console asks "Gegenrede?". The chair either records "no objection, accepted" (GO § 15 Abs. 8) or gives the floor to one objection and starts the GO motion poll. Afterwards the interrupted phase continues.

## Speaking timer

- Starts with the first voice packet of the speaker after being moved into "Redner" or "Antragsteller"; if the speaker has not spoken after 20 s, the console offers "start now".
- Default times as in the rules table, overridable per speech (one click: +1 min, set exact) and per motion (GO § 15h: whole minutes only, applies to all following speeches of the current motion, reset when the next agenda item is selected).
- Warnings by Mumble text message to the speaker: 30 s before the end ("Noch 30 Sekunden") and at 0 ("Die Redezeit ist abgelaufen"). Text is configurable.
- Expiry turns the timer red in console and overlay and counts the overrun. Nobody is moved; the chair clicks "done".
- Pause and resume, for interruptions by the chair.

## OpenSlides integration

### Connection and rights

The existing local OpenSlides account of the motion push (options `openslides.url`, `openslides.username`, `openslides.password`; not SSO), additionally member of a group in the meeting with `poll.can_manage` (polls on topics), `motion.can_manage_polls` (polls on motions), `agenda_item.can_manage` (procedural topics) and, if the projector is driven, `projector.can_manage`. It must be a real user with a login session: `poll.start`, `poll.stop` and `poll.publish` call the vote service with the caller's token and refresh cookie. The existing client already handles login, the token in the `Authentication` header, the `refreshId` cookie and retry on 401; the new part is the long-lived autoupdate subscription.

### API facts

Verified against OpenSlides 4.3.2 (openslides-backend, openslides-autoupdate-service, openslides-vote-service, openslides-auth-service, openslides-client, openslides-meta). Production runs 4.3.5 (released 09.10.2026); 4.3.3 to 4.3.5 contain only bug fixes and a scope restriction for basic user data, none of which touches polls, actions or autoupdate.

- Auth: `POST /system/auth/login` returns the access token in the `authentication` header (valid 600 s) and the cookie `refreshId` (30 days, `Secure`). Every service refreshes an expired token transparently if the cookie is sent along and returns the new token in the `Authentication` response header.
- Actions: `POST /system/action/handle_request` with `[{"action": ..., "data": [...]}]`; `poll.create` returns `[[{"id": N, "sequential_number": M}]]`.
- `poll.create` requires `title`, `type`, `pollmethod`, `meeting_id`, `options` and in practice `content_object_id`. Optional: `onehundred_percent_base`, `entitled_group_ids`, `backend` ("fast"), `min_votes_amount`, `max_votes_amount`, `max_votes_per_option`, `global_yes/no/abstain`, `live_voting_enabled`. There is no "start immediately" flag; the tool sends `poll.start` right after.
  - Topic poll: `content_object_id = "topic/<id>"`, `options = [{"text": "..."}]`. A topic poll with a single option fails in the OpenSlides client, so every procedural Yes/No poll has the two options "Ja" and "Nein" with `pollmethod = "Y"`, `max_votes_amount = 1` and no global abstain; abstaining means not voting, as in 2026.1. Support and controversy polls use the same shape and only count "Ja". A poll between competing motions is an acceptance vote (GO § 7, § 14): one option per motion (`<code> <title>`), `pollmethod = "Y"`, `max_votes_per_option = 1`, `max_votes_amount` = number of options, and `global_no = true` labelled "keinen der Anträge" as the valid empty ballot that rejects all. The dry run confirms that the OpenSlides client shows a topic poll with several votes per ballot; if it does not, the poll falls back to one vote per member.
  - Motion poll (final vote): `pollmethod = "YNA"`, Ja/Nein/Enthaltung as in 2026.1.
  - Motion poll: `content_object_id = "motion/<id>"`, `options = [{"content_object_id": "motion/<id>"}]`; the motion's state must allow creating polls.
  - `type`: always `pseudoanonymous`, for topic and motion polls. `cryptographic` exists only in the schema. Named and pseudoanonymous need electronic voting enabled for the organisation.
  - `entitled_group_ids = [<delegates group id>]` on every poll, so the per-poll group selection that was a manual step in 2026.1 is always right.
- State machine: `created` -> `poll.start` -> `started` -> `poll.stop` -> `finished` -> `poll.publish` -> `published` (publish also works from `started` and stops first). `poll.stop` fetches the ballots and fills `option.yes/no/abstain`, `votesvalid` and `votescast`. `poll.reset` and `poll.delete` have no state guard in the backend; the tool offers them only for polls it created and asks for confirmation.
- Procedural topic: `topic.create` with `meeting_id`, `title`, `agenda_parent_id = <motion agenda item id>`, `agenda_type = "common"`, optional `agenda_weight`.
- Autoupdate: `POST /system/autoupdate` with header `Authentication: bearer <jwt>` and cookie `refreshId` (both required). Body: a JSON array of `{ids, collection, fields}`; `fields` maps a field to `null` or to a nested follow `{type: "relation" | "relation-list" | ..., collection, fields}`. The response is newline-delimited JSON: first the full key map (`"poll/7/state": "started"`), then changed keys only, `null` for removed values, and an empty `{}` heartbeat every 5 minutes. An open stream is not re-authenticated; after a drop the tool reconnects with the latest token and cookie. The `position` parameter from the README does not exist in 4.3.2.
- Subscription: meeting -> `poll_ids` (title, state, type, pollmethod, content_object_id, votesvalid, votescast, votesinvalid, `live_votes`, option_ids -> yes/no/abstain/text, global_option_id) and meeting -> `agenda_item_ids` (item number, parent, weight, content object id with its title, motion number and agenda item id), plus the voting countdown (`poll_countdown_id` -> running, countdown_time, default_time).
- Live count: there is no vote count endpoint in 4.3.2. The calculated field `poll/<id>/live_votes` is a map with one key per user who has voted (values `null` except for named polls with live voting); the count is the number of keys, exactly as the OpenSlides client shows it. It is readable only while the poll is `started` and only with the manage right for that poll, which the tool account has.
- Voting countdown: with `poll_couple_countdown` (meeting setting, default on), `poll.start` restarts the meeting's "Voting" projector countdown with its `default_time` (default 60 s), and stop or publish resets it. Changing the time is `projector_countdown.update` with `running` and `countdown_time` (absolute end time while running); there are no separate start or stop actions.
- Projection (optional): `projector.project` with `ids` (projector ids), `meeting_id`, `content_object_id` (`motion/<id>`, `poll/<id>`, `projector_countdown/<id>`, ...) and `type: "poll"` for polls.
- Voting itself stays with the members: a vote counts only for users who are present and in an entitled group.

### Procedural topics

Procedural polls cannot hang on a motion's agenda item. Every motion therefore gets a plain topic "<Code>: Verfahren" as a child of its agenda item. The tool creates it the first time it is needed (or for all motions at once from the console before the assembly). The motion portal push can create them together with the motions.

### Poll templates

One click creates the poll on the right topic or motion, selects the `delegates` group, and starts it (if "auto start" is on). Titles carry the motion code so that the minutes can be reconstructed from OpenSlides alone.

```
Template                     Attached to         Options     Evaluation shown
Debatte fortsetzen?          procedural topic    Ja/Nein     Ja > Nein ("fortsetzen")
GO-Antrag: <text>            procedural topic    Ja/Nein     Ja > Nein
Meinungsbild: <question>     procedural topic    Ja/Nein     numbers only
Unterstützung <name/motion>  procedural topic    Ja/Nein     Ja >= N (N = 10 or 20)
Kontroverse <motion>         procedural topic    Ja/Nein     Ja >= 10 -> taken out of the block
Konkurrenz <A> / <B>         procedural topic    A, B, ...   highest Ja wins if Ja > half of the valid ballots;
                                                             else runoff (one vote, "keinen der Anträge")
Schlussabstimmung            motion              J/N/Enth.   simple: Yes > No; 2/3: Yes >= 2 * No
Blockabstimmung              procedural topic    Ja/Nein     majority of the block's motions
```

### Poll timer

The poll timer is the OpenSlides voting countdown, so projector, OpenSlides client, console and overlay show the same time. The default poll duration is 70 s (60 s plus 10 s for the stream delay). `poll.start` restarts the countdown with the meeting default; if that differs from the tool's duration, the tool sets it right after the start with `projector_countdown.update`. "+30 s" extends it the same way. The overlay shows "Abstimmung läuft" with the title, the countdown and the number of votes cast so far (`live_votes`). When the countdown reaches 0, the tool sends `poll.stop` ("auto stop", on by default), and then `poll.publish` ("auto publish", on by default; off means publishing is a click by the chair). Duration, auto start, auto stop and auto publish are changed in the console's poll panel during the session; the stored options are only the defaults for a new session. When results arrive, the overlay shows Yes, No, Abstain and the evaluation, for example "angenommen (2/3 erreicht: 34 zu 15)". The evaluation is advisory; the chair announces the result.

Polls the chair creates by hand in OpenSlides are picked up the same way (timer, overlay, result), they just were not created by the tool.

## Chair console

One page for the chair and helpers, laid out for one 1920 px screen next to Mumble:

- Header: current agenda item and phase, round counters, list status (open, closes at 11:30, closed), connection status of Mumble and OpenSlides.
- Centre: the current speaker with timer and speaking indicator; buttons "done", "+1 min", "pause".
- Queues side by side (GO-Antrag, Frage, Pro, Contra), each with names and waiting times; the suggested next speaker is highlighted; buttons "give the floor", "skip", "dismiss".
- Poll panel: duration and the auto start, auto stop and auto publish switches; template buttons for the current item, the running poll with countdown and live count, the last results.
- Log: the last chair actions, with undo for "dismiss" and "skip".

Keyboard shortcuts for the frequent actions (give the floor to the suggestion, done, +1 min). A read-only variant of the page for the minutes taker.

## Overlay

A page at `/assembly/<event>/overlay`, transparent background, 1920x1080, designed to sit over the OBS scene "Versammlung":

- Lower third: speaker name (Mumble name), role badge (Pro, Contra, Frage, GO-Antrag, Antragsteller, Kandidat, Versammlungsleitung), remaining time as a bar that turns orange at 30 s and red at 0.
- Optional side column: next two names per side.
- Poll banner: title, countdown, live count, then the result with evaluation for 15 s.
- Current agenda item and motion code in a small header.

It contains only what the audience sees anyway and needs no login. Fonts and colours follow the OBS scene collection of 2026.1. The fonts are the party's house fonts from the Piratenwiki page "Pirate Design/Schriften": PoliticsHead v1.9 (headlines, capitals only) and DejaRip 1.2 (text, regular and bold), served by Quartermaster as OTF.

## Permissions

A new chapter-scoped permission `assembly_chair` ("Versammlungsleitung"), granted for the event's chapter to the chair and the chair helpers for the day. The settings page (Mumble connection, channel mapping, OpenSlides account) needs the global admin permission.

## Configuration

DB-backed options in the existing pattern:

```
mumble.host, mumble.port              Bundesmumble address
mumble.username                       bot name
mumble.certificate                    PKCS#12 as Base64, masked; password masked
mumble.channels.*                     channel id per role
assembly.times.*                      default times (presentation, speech, question, statement, closing word,
                                      candidate presentation, candidate answer)
assembly.grace_seconds                queue grace period
assembly.voice_hangover_ms            speaking detection hangover
assembly.messages.*                   warning texts
openslides.*                          existing connection options of the motion push, reused
assembly.openslides.delegates_group_id   entitled group for every poll
assembly.poll.*                       defaults for duration (70 s), auto start, auto stop (on), auto publish (on)
```

The OpenSlides meeting id comes from the event's motion portal.

## Data

Session state lives in memory. Persisted:

- `AssemblySession`: event, OpenSlides meeting id, started, ended.
- `AssemblyLogEntry`: timestamp, kind (speech, poll, phase change, chair action), agenda item, Mumble name, role, duration, poll id and result. Export as CSV and Markdown for the minutes taker; deleted on request after the minutes are signed.

## Failure and recovery

- Mumble connection lost: reconnect with backoff (1 s up to 30 s). Queues are kept in memory; after reconnect they are reconciled with the actual channel occupancy (known members keep their position, unknown ones are appended in channel order and marked "Reihenfolge unsicher").
- Quartermaster restarted: queues are rebuilt from occupancy as above, the current speaker is whoever sits in "Redner", timers restart from the log entry of the current speech.
- OpenSlides unavailable: poll buttons are disabled with a notice; queues and timers keep working. Polls are then run by hand.
- Bot lacks a right: the move fails with a Mumble "permission denied"; the console shows it and the chair moves by hand.
- In every case the chair can continue by hand in Mumble and OpenSlides; the tool catches up with what it observes.

## Privacy

The tool processes Mumble names (chosen freely by members), channel changes and the timing of voice packets, and OpenSlides poll data, all of which are visible to the assembly anyway. No audio is stored or decoded. The session log exists for the minutes and is deleted after the minutes are signed. No member data from Quartermaster is shown on the overlay.

## Testing

- Session logic (queues, alternation, counters, phases, timers, evaluation) as unit tests against a fake clock and a fake `IMumbleConnection`.
- Mumble connection by hand against the Bundesmumble, in a separate test channel tree that copies the "Bundesparteitag" tree, never in the real assembly channels. No local test server.
- OpenSlides through the existing fake message handler; poll payloads checked against recorded real requests.
- Dry run before the assembly on the Bundesmumble in a copy of the channel tree with at least four people without admin rights and a test meeting in OpenSlides, together with the chair helpers.

## Build order for 28.11.2026

Must, ready for the dry run around 21.11.:

1. Mumble connection, channel model, moves, text messages, speaking detection.
2. Queues, console with give the floor and done, speaking timer with warnings.
3. Overlay with speaker, role and time.

Should:

4. OpenSlides live poll status, poll timer with auto stop, result evaluation, overlay poll banner.
5. Phases and suggestions (rounds, alternation, list closing at a set time).

Could:

6. Poll templates on one click, procedural topics, election flow.
7. Session log export, read-only minutes view, projector countdown.

Later: OBS scene switching through obs-websocket, subtitles, a projector view generated by the tool.

## Open questions

- The exact ACL entries for the bot (registration, Move on "Bundesparteitag" and the test tree); the user sets them up before the first connection.
