using System.Collections.Generic;
using System.Text.Json;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>Body of the autoupdate request: polls with options, agenda items with their content objects, and the voting countdown of one meeting.</summary>
public static class OpenSlidesSubscription {
    public static string Build(int meetingId) {
        var option = new Dictionary<string, object?> {
            ["id"] = null,
            ["text"] = null,
            ["yes"] = null,
            ["no"] = null,
            ["abstain"] = null,
            ["content_object_id"] = null
        };
        var poll = new Dictionary<string, object?> {
            ["id"] = null,
            ["title"] = null,
            ["state"] = null,
            ["type"] = null,
            ["pollmethod"] = null,
            ["content_object_id"] = null,
            ["votesvalid"] = null,
            ["votescast"] = null,
            ["votesinvalid"] = null,
            ["live_votes"] = null,
            ["option_ids"] = Follow("relation-list", "option", option),
            ["global_option_id"] = Follow("relation", "option", option)
        };
        var agendaItem = new Dictionary<string, object?> {
            ["id"] = null,
            ["item_number"] = null,
            ["parent_id"] = null,
            ["weight"] = null,
            ["content_object_id"] = new Dictionary<string, object?> {
                ["type"] = "generic-relation",
                ["fields"] = new Dictionary<string, object?> { ["id"] = null, ["title"] = null, ["number"] = null }
            }
        };
        var countdown = new Dictionary<string, object?> {
            ["id"] = null,
            ["running"] = null,
            ["countdown_time"] = null,
            ["default_time"] = null
        };

        var request = new[] {
            new Dictionary<string, object?> {
                ["ids"] = new[] { meetingId },
                ["collection"] = "meeting",
                ["fields"] = new Dictionary<string, object?> {
                    ["id"] = null,
                    ["poll_ids"] = Follow("relation-list", "poll", poll),
                    ["agenda_item_ids"] = Follow("relation-list", "agenda_item", agendaItem),
                    ["poll_countdown_id"] = Follow("relation", "projector_countdown", countdown)
                }
            }
        };

        return JsonSerializer.Serialize(request);
    }

    private static Dictionary<string, object?> Follow(string type, string collection, Dictionary<string, object?> fields) {
        return new Dictionary<string, object?> {
            ["type"] = type,
            ["collection"] = collection,
            ["fields"] = fields
        };
    }
}
