using System;
using System.Collections.Generic;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// One outbound message. <see cref="ChannelAddress"/> is channel-specific (email,
/// chat id, postal address); <see cref="Metadata"/> carries per-channel extras
/// (e.g. <c>"TemplateIdentifier"</c> for email's audit log) — unknown keys are ignored.
/// </summary>
public record ChannelMessage(
    string ChannelAddress,
    string Subject,
    string Body,
    string? SourceEntityType = null,
    Guid? SourceEntityId = null,
    IReadOnlyDictionary<string, string>? Metadata = null
);
