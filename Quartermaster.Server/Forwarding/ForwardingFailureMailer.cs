using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Forwarding;

/// <summary>Sends the failure mail of a step that ran out of attempts to the failure address of its target chapter, looked up the chapter tree.</summary>
public class ForwardingFailureMailer {
    private readonly EmailMessageChannel _email;
    private readonly ForwardingTicketRenderer _renderer;
    private readonly ForwardingSettingsResolver _resolver;
    private readonly ILogger<ForwardingFailureMailer> _logger;

    public ForwardingFailureMailer(EmailMessageChannel email, ForwardingTicketRenderer renderer, ForwardingSettingsResolver resolver, ILogger<ForwardingFailureMailer> logger) {
        _email = email;
        _renderer = renderer;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task SendAsync(ForwardingCase forwardingCase, ForwardingStep step, RenderedTicket? ticket, CancellationToken ct) {
        var address = _resolver.FailureEmailFor(step.TargetChapterId);
        if (address == null) {
            _logger.LogWarning("Forwarding case {Reference} failed, but no failure address is set for its chapter or any parent", forwardingCase.Reference);
            return;
        }

        try {
            var (subject, html) = await _renderer.RenderFailureMailAsync(forwardingCase, step, ticket);
            await _email.SendAsync(new ChannelMessage(
                ChannelAddress: address,
                Subject: subject,
                Body: html,
                SourceEntityType: ForwardingCaseRepository.MailSourceType,
                SourceEntityId: forwardingCase.Id,
                Metadata: new Dictionary<string, string> {
                    [NotificationLogMetadataKeys.TriggerId] = "forwarding_failed",
                    [NotificationLogMetadataKeys.TemplateIdentifier] = ForwardingTicketRenderer.FailureMailTemplate
                }), ct);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogError(ex, "Sending the failure mail of forwarding case {Reference} failed", forwardingCase.Reference);
        }
    }
}
