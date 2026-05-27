using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Rendering;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Resolves recipients for a trigger, renders the email template per recipient, consults
/// each user's preferences, and hands accepted messages to the email channel. Phase 3
/// gates email per (user, trigger); Phase 4 will fan out to additional channels.
/// </summary>
public class NotificationDispatcher {
    private readonly Dictionary<string, IRecipientResolver> _resolvers;
    private readonly EmailMessageChannel _emailChannel;
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserNotificationPreferenceRepository _prefRepo;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<IRecipientResolver> resolvers,
        EmailMessageChannel emailChannel,
        OptionRepository optionRepo,
        ChapterRepository chapterRepo,
        UserNotificationPreferenceRepository prefRepo,
        ILogger<NotificationDispatcher> logger) {
        _resolvers = resolvers.ToDictionary(r => r.TriggerId, r => r);
        _emailChannel = emailChannel;
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _prefRepo = prefRepo;
        _logger = logger;
    }

    /// <summary>
    /// Fan out a notification: resolve recipients, render the template, hand off to email.
    /// <paramref name="modelFactory"/> builds the per-recipient Fluid template model
    /// (lets callers personalize subject/body per user).
    /// </summary>
    public async Task DispatchAsync(
        string triggerId,
        object payload,
        Func<NotificationRecipient, Dictionary<string, object>> modelFactory,
        string? sourceEntityType = null,
        Guid? sourceEntityId = null,
        CancellationToken ct = default) {

        if (!_resolvers.TryGetValue(triggerId, out var resolver)) {
            _logger.LogWarning("No recipient resolver registered for trigger {Trigger}", triggerId);
            return;
        }

        var recipients = resolver.Resolve(payload);
        if (recipients.Count == 0)
            return;

        var subjectKey = $"notifications.{triggerId}.email.subject";
        var bodyKey = $"notifications.{triggerId}.email.body";
        var subjectTemplate = _optionRepo.ResolveValue(subjectKey, null, _chapterRepo);
        var bodyTemplate = _optionRepo.ResolveValue(bodyKey, null, _chapterRepo);

        if (string.IsNullOrEmpty(subjectTemplate) || string.IsNullOrEmpty(bodyTemplate)) {
            _logger.LogWarning(
                "Notification template missing for trigger {Trigger} (keys: {SubjectKey} / {BodyKey})",
                triggerId, subjectKey, bodyKey);
            return;
        }

        foreach (var recipient in recipients) {
            if (!IsEmailEnabledFor(recipient, triggerId))
                continue;

            var model = modelFactory(recipient);
            var (renderedSubject, subjectError) = await TemplateRenderer.RenderAsync(subjectTemplate, model);
            var (renderedBody, bodyError) = await TemplateRenderer.RenderAsync(bodyTemplate, model);
            if (subjectError != null || bodyError != null) {
                _logger.LogWarning(
                    "Template render error for trigger {Trigger} recipient {Address}: subject={SubjectErr} body={BodyErr}",
                    triggerId, recipient.ChannelAddress, subjectError, bodyError);
            }

            var metadata = new Dictionary<string, string> {
                [EmailMessageChannel.TriggerIdMetadataKey] = triggerId,
                [EmailMessageChannel.TemplateIdentifierMetadataKey] = bodyKey
            };
            if (recipient.UserId.HasValue)
                metadata[EmailMessageChannel.RecipientUserIdMetadataKey] = recipient.UserId.Value.ToString();

            await _emailChannel.SendAsync(new ChannelMessage(
                ChannelAddress: recipient.ChannelAddress,
                Subject: renderedSubject ?? subjectTemplate,
                Body: renderedBody ?? bodyTemplate,
                SourceEntityType: sourceEntityType,
                SourceEntityId: sourceEntityId,
                Metadata: metadata), ct);
        }
    }

    /// <summary>
    /// Anonymous recipients (no user id) follow the channel default; identified users
    /// follow their explicit override, or the default when none is set.
    /// </summary>
    private bool IsEmailEnabledFor(NotificationRecipient recipient, string triggerId) {
        var channelId = EmailMessageChannel.ChannelId;
        var defaultValue = NotificationDefaults.IsEnabledByDefault(channelId);
        if (recipient.UserId == null)
            return defaultValue;
        return _prefRepo.IsEnabled(recipient.UserId.Value, triggerId, channelId, defaultValue);
    }
}
