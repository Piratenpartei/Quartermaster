using System;
using System.Linq;
using System.Security.Cryptography;
using LinqToDB;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Notifications;

/// <summary>
/// CRUD + consume for Telegram link tokens. <see cref="Consume"/> is transactional:
/// it marks the token consumed AND writes the chat id onto the user in one step so
/// we never end up with a consumed token whose chat id was lost.
/// </summary>
public class TelegramLinkTokenRepository {
    /// <summary>Tokens older than this are considered expired and refused on consume.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly DbContext _context;

    public TelegramLinkTokenRepository(DbContext context) {
        _context = context;
    }

    public TelegramLinkToken Create(Guid userId, DateTime now) {
        var token = GenerateToken();
        var row = new TelegramLinkToken {
            Token = token,
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now + TokenLifetime,
            ConsumedAt = null
        };
        _context.Insert(row);
        return row;
    }

    public TelegramLinkToken? Get(string token) {
        return _context.TelegramLinkTokens.FirstOrDefault(t => t.Token == token);
    }

    /// <summary>
    /// Validates the token, marks it consumed, and writes <paramref name="chatId"/>
    /// to the user. Returns the user id linked, or null if the token is unknown,
    /// expired, or already consumed.
    /// </summary>
    public Guid? Consume(string token, string chatId, DateTime now) {
        using var tx = _context.BeginTransaction();
        var row = _context.TelegramLinkTokens.FirstOrDefault(t => t.Token == token);
        if (row == null) {
            return null;
        }
        if (row.ConsumedAt != null) {
            return null;
        }
        if (row.ExpiresAt < now) {
            return null;
        }
        _context.TelegramLinkTokens
            .Where(t => t.Token == token)
            .Set(t => t.ConsumedAt, now)
            .Update();
        _context.GetTable<User>()
            .Where(u => u.Id == row.UserId)
            .Set(u => u.TelegramChatId, chatId)
            .Update();
        tx.Commit();
        return row.UserId;
    }

    /// <summary>Clears the user's link: removes the chat id and deletes any unconsumed tokens.</summary>
    public void Unlink(Guid userId) {
        using var tx = _context.BeginTransaction();
        _context.TelegramLinkTokens.Where(t => t.UserId == userId && t.ConsumedAt == null).Delete();
        _context.GetTable<User>()
            .Where(u => u.Id == userId)
            .Set(u => u.TelegramChatId, (string?)null)
            .Update();
        tx.Commit();
    }

    public int PurgeExpired(DateTime now) {
        return _context.TelegramLinkTokens
            .Where(t => t.ConsumedAt == null && t.ExpiresAt < now)
            .Delete();
    }

    private static string GenerateToken() {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
