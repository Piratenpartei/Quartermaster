using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Quartermaster.Server.NewsPosts.Postiz;

public static class PostizRequestBuilder {
    public static JsonObject Build(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, DateTime nowUtc, string xReplySetting) {
        var posts = new JsonArray();
        foreach (var channel in channels) {
            var settings = new JsonObject { ["__type"] = channel.Identifier };
            if (channel.Identifier == "x") {
                settings["who_can_reply_post"] = xReplySetting;
            }

            posts.Add(new JsonObject {
                ["integration"] = new JsonObject { ["id"] = channel.Id },
                ["value"] = new JsonArray(new JsonObject { ["content"] = text, ["image"] = new JsonArray() }),
                ["settings"] = settings
            });
        }

        var date = scheduledForUtc ?? nowUtc;
        return new JsonObject {
            ["type"] = scheduledForUtc == null ? "now" : "schedule",
            ["date"] = date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["shortLink"] = false,
            ["tags"] = new JsonArray(),
            ["posts"] = posts
        };
    }
}
