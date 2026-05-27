using System;

namespace Quartermaster.Api.Notifications;

public class TelegramLinkStatusDTO {
    public bool Linked { get; set; }
    public string? ChatId { get; set; }
}

/// <summary>Returned by the link-start endpoint. <see cref="Deeplink"/> is null when the bot username isn't configured.</summary>
public class TelegramLinkStartDTO {
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string? Deeplink { get; set; }
    public string? BotUsername { get; set; }
}
