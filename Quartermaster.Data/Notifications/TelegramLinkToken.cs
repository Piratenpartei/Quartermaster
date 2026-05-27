using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Notifications;

/// <summary>
/// Short-lived single-use token a user generates from the account page; the Telegram
/// receiver consumes it on <c>/start &lt;token&gt;</c> and binds the originating chat id
/// to <see cref="Quartermaster.Data.Users.User.TelegramChatId"/>. Tokens are pruned
/// after consumption or expiry.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class TelegramLinkToken {
    public const string TableName = "TelegramLinkTokens";

    [PrimaryKey]
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
