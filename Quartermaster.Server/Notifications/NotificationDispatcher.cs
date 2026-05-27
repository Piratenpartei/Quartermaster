using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Resolves recipients for a trigger and fans out to every configured channel,
/// gated by each user's (trigger × channel) preference. Per-channel template
/// keys are <c>notifications.{trigger}.{channel}.subject|body</c>; a channel
/// with no body template is skipped for that trigger.
/// </summary>
public class NotificationDispatcher {
    private readonly Dictionary<string, IRecipientResolver> _resolvers;
    private readonly EmailMessageChannel _emailChannel;
    private readonly TelegramMessageChannel _telegramChannel;
    private readonly DbContext _db;
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserNotificationPreferenceRepository _prefRepo;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<IRecipientResolver> resolvers,
        EmailMessageChannel emailChannel,
        TelegramMessageChannel telegramChannel,
        DbContext db,
        OptionRepository optionRepo,
        ChapterRepository chapterRepo,
        UserNotificationPreferenceRepository prefRepo,
        ILogger<NotificationDispatcher> logger) {
        _resolvers = resolvers.ToDictionary(r => r.TriggerId, r => r);
        _emailChannel = emailChannel;
        _telegramChannel = telegramChannel;
        _db = db;
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _prefRepo = prefRepo;
        _logger = logger;
    }

    /// <summary>
    /// Fan out a notification: resolve recipients, render per-channel templates,
    /// then for each (recipient × channel) consult preferences and hand off the
    /// rendered message. <paramref name="modelFactory"/> builds the per-recipient
    /// Fluid model.
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
        if (recipients.Count == 0) {
            return;
        }

        var channels = DispatchableChannels();
        var perChannelTemplates = LoadTemplates(triggerId, channels);

        var telegramAddressByUserId = LoadTelegramAddresses(recipients, channels);

        foreach (var recipient in recipients) {
            foreach (var channel in channels) {
                if (!_prefAllows(recipient, triggerId, channel.Id)) {
                    continue;
                }
                var address = ResolveAddress(channel.Id, recipient, telegramAddressByUserId);
                if (string.IsNullOrEmpty(address)) {
                    continue;
                }
                var (subjectTpl, bodyTpl) = perChannelTemplates[channel.Id];
                if (string.IsNullOrEmpty(bodyTpl)) {
                    continue;
                }
                var model = modelFactory(recipient);
                var renderedSubject = await RenderOptional(subjectTpl, model, triggerId, channel.Id, recipient);
                var renderedBody = await RenderRequired(bodyTpl, model, triggerId, channel.Id, recipient);

                var metadata = new Dictionary<string, string> {
                    [NotificationLogMetadataKeys.TriggerId] = triggerId,
                    [NotificationLogMetadataKeys.TemplateIdentifier] = $"notifications.{triggerId}.{channel.Id}.body"
                };
                if (recipient.UserId.HasValue) {
                    metadata[NotificationLogMetadataKeys.RecipientUserId] = recipient.UserId.Value.ToString();
                }

                await channel.SendAsync(new ChannelMessage(
                    ChannelAddress: address,
                    Subject: renderedSubject ?? "",
                    Body: renderedBody,
                    SourceEntityType: sourceEntityType,
                    SourceEntityId: sourceEntityId,
                    Metadata: metadata), ct);
            }
        }
    }

    private List<IMessageChannel> DispatchableChannels() {
        var channels = new List<IMessageChannel>();
        if (_emailChannel.IsConfigured) {
            channels.Add(_emailChannel);
        }
        if (_telegramChannel.IsConfigured) {
            channels.Add(_telegramChannel);
        }
        return channels;
    }

    private Dictionary<string, (string? Subject, string? Body)> LoadTemplates(string triggerId, List<IMessageChannel> channels) {
        var result = new Dictionary<string, (string?, string?)>();
        foreach (var channel in channels) {
            var subject = _optionRepo.ResolveValue($"notifications.{triggerId}.{channel.Id}.subject", null, _chapterRepo);
            var body = _optionRepo.ResolveValue($"notifications.{triggerId}.{channel.Id}.body", null, _chapterRepo);
            result[channel.Id] = (subject, body);
            if (string.IsNullOrEmpty(body)) {
                _logger.LogWarning(
                    "Notification body template missing for {Trigger} on {Channel} (key: notifications.{Trigger}.{Channel}.body)",
                    triggerId, channel.Id, triggerId, channel.Id);
            }
        }
        return result;
    }

    private Dictionary<Guid, string> LoadTelegramAddresses(IReadOnlyList<NotificationRecipient> recipients, List<IMessageChannel> channels) {
        if (!channels.Any(c => c.Id == TelegramMessageChannel.ChannelId)) {
            return new Dictionary<Guid, string>();
        }
        var userIds = recipients
            .Where(r => r.UserId.HasValue)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToList();
        if (userIds.Count == 0) {
            return new Dictionary<Guid, string>();
        }
        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.TelegramChatId != null)
            .Select(u => new { u.Id, u.TelegramChatId })
            .ToDictionary(x => x.Id, x => x.TelegramChatId!);
    }

    private static string? ResolveAddress(string channelId, NotificationRecipient recipient, Dictionary<Guid, string> telegramAddresses) {
        if (channelId == EmailMessageChannel.ChannelId) {
            return recipient.ChannelAddress;
        }
        if (channelId == TelegramMessageChannel.ChannelId) {
            if (recipient.UserId == null) {
                return null;
            }
            return telegramAddresses.TryGetValue(recipient.UserId.Value, out var addr) ? addr : null;
        }
        return null;
    }

    private async Task<string?> RenderOptional(string? template, Dictionary<string, object> model, string triggerId, string channelId, NotificationRecipient recipient) {
        if (string.IsNullOrEmpty(template)) {
            return null;
        }
        var (text, error) = await TemplateRenderer.RenderAsync(template, model);
        if (error != null) {
            _logger.LogWarning("Template render error for {Trigger}/{Channel} recipient {Address}: {Error}",
                triggerId, channelId, recipient.ChannelAddress, error);
        }
        return text ?? template;
    }

    private async Task<string> RenderRequired(string template, Dictionary<string, object> model, string triggerId, string channelId, NotificationRecipient recipient) {
        var (text, error) = await TemplateRenderer.RenderAsync(template, model);
        if (error != null) {
            _logger.LogWarning("Template render error for {Trigger}/{Channel} recipient {Address}: {Error}",
                triggerId, channelId, recipient.ChannelAddress, error);
        }
        return text ?? template;
    }

    private bool _prefAllows(NotificationRecipient recipient, string triggerId, string channelId) {
        var defaultValue = NotificationDefaults.IsEnabledByDefault(channelId);
        if (recipient.UserId == null) {
            return defaultValue;
        }
        return _prefRepo.IsEnabled(recipient.UserId.Value, triggerId, channelId, defaultValue);
    }
}
