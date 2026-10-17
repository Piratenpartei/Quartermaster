using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

public record OpenSlidesPollOptionView(int Id, string? Text, string? ContentObjectId, decimal Yes, decimal No, decimal Abstain);

public record OpenSlidesPollView(int Id, string Title, string State, string Type, string PollMethod, string ContentObjectId,
    decimal? VotesValid, decimal? VotesCast, int LiveVoteCount, IReadOnlyList<OpenSlidesPollOptionView> Options, OpenSlidesPollOptionView? GlobalOption);

public record OpenSlidesAgendaItemView(int Id, string? ItemNumber, int? ParentId, int Weight, string ContentObjectId, string Title, string? MotionNumber);

/// <summary><see cref="CountdownTime"/> is the absolute end in Unix seconds while running, the remaining seconds otherwise.</summary>
public record OpenSlidesCountdownView(int Id, bool Running, double CountdownTime, int DefaultTime);

public record OpenSlidesMeetingView(IReadOnlyList<OpenSlidesPollView> Polls, IReadOnlyList<OpenSlidesAgendaItemView> AgendaItems, OpenSlidesCountdownView? PollCountdown) {
    public static OpenSlidesMeetingView Empty { get; } = new([], [], null);

    public static OpenSlidesMeetingView From(OpenSlidesKeyStore store, int meetingId) {
        var reader = new KeyReader(store);
        var polls = reader.Ids($"meeting/{meetingId}/poll_ids")
            .Select(reader.Poll)
            .Where(p => p != null)
            .Select(p => p!)
            .OrderBy(p => p.Id)
            .ToList();
        var agendaItems = reader.Ids($"meeting/{meetingId}/agenda_item_ids")
            .Select(reader.AgendaItem)
            .Where(a => a != null)
            .Select(a => a!)
            .ToList();
        var countdownId = reader.Int($"meeting/{meetingId}/poll_countdown_id");

        return new OpenSlidesMeetingView(polls, agendaItems, countdownId == null ? null : reader.Countdown(countdownId.Value));
    }

    private class KeyReader {
        private readonly OpenSlidesKeyStore _store;

        public KeyReader(OpenSlidesKeyStore store) {
            _store = store;
        }

        public OpenSlidesPollView? Poll(int id) {
            var state = String($"poll/{id}/state");
            if (state == null)
                return null;

            var globalOptionId = Int($"poll/{id}/global_option_id");
            return new OpenSlidesPollView(
                id,
                String($"poll/{id}/title") ?? "",
                state,
                String($"poll/{id}/type") ?? "",
                String($"poll/{id}/pollmethod") ?? "",
                String($"poll/{id}/content_object_id") ?? "",
                Decimal($"poll/{id}/votesvalid"),
                Decimal($"poll/{id}/votescast"),
                LiveVoteCount($"poll/{id}/live_votes"),
                Ids($"poll/{id}/option_ids").Select(Option).ToList(),
                globalOptionId == null ? null : Option(globalOptionId.Value));
        }

        public OpenSlidesAgendaItemView? AgendaItem(int id) {
            var contentObject = String($"agenda_item/{id}/content_object_id");
            if (contentObject == null)
                return null;

            return new OpenSlidesAgendaItemView(
                id,
                String($"agenda_item/{id}/item_number"),
                Int($"agenda_item/{id}/parent_id"),
                Int($"agenda_item/{id}/weight") ?? 0,
                contentObject,
                String($"{contentObject}/title") ?? "",
                contentObject.StartsWith("motion/") ? String($"{contentObject}/number") : null);
        }

        public OpenSlidesCountdownView? Countdown(int id) {
            var running = _store.Get($"projector_countdown/{id}/running");
            if (running == null)
                return null;

            return new OpenSlidesCountdownView(
                id,
                running.Value.ValueKind == JsonValueKind.True,
                Double($"projector_countdown/{id}/countdown_time") ?? 0,
                Int($"projector_countdown/{id}/default_time") ?? 0);
        }

        public List<int> Ids(string key) {
            var value = _store.Get(key);
            if (value == null || value.Value.ValueKind != JsonValueKind.Array)
                return [];

            return value.Value.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetInt32())
                .ToList();
        }

        public int? Int(string key) {
            var value = _store.Get(key);
            if (value == null || value.Value.ValueKind != JsonValueKind.Number)
                return null;

            return value.Value.TryGetInt32(out var number) ? number : null;
        }

        private OpenSlidesPollOptionView Option(int id) {
            return new OpenSlidesPollOptionView(
                id,
                String($"option/{id}/text"),
                String($"option/{id}/content_object_id"),
                Decimal($"option/{id}/yes") ?? 0,
                Decimal($"option/{id}/no") ?? 0,
                Decimal($"option/{id}/abstain") ?? 0);
        }

        private string? String(string key) {
            var value = _store.Get(key);
            return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        }

        private double? Double(string key) {
            var value = _store.Get(key);
            return value?.ValueKind == JsonValueKind.Number ? value.Value.GetDouble() : null;
        }

        // OpenSlides sends vote counts as decimal strings ("12.000000"); numbers are accepted too.
        private decimal? Decimal(string key) {
            var value = _store.Get(key);
            if (value == null)
                return null;
            if (value.Value.ValueKind == JsonValueKind.Number)
                return value.Value.GetDecimal();
            if (value.Value.ValueKind != JsonValueKind.String)
                return null;

            return decimal.TryParse(value.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }

        // live_votes holds one key per user who voted; the count is the number of keys, as in the OpenSlides client.
        private int LiveVoteCount(string key) {
            var value = _store.Get(key);
            return value?.ValueKind == JsonValueKind.Object ? value.Value.EnumerateObject().Count() : 0;
        }
    }
}
