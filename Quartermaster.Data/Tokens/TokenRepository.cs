using LinqToDB;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Tokens;

public class TokenRepository {
    private const int DefaultTokenLifetimeDays = 7;

    private readonly DbContext _context;
    private readonly OptionRepository _options;
    private readonly ChapterRepository _chapters;

    public TokenRepository(DbContext context, OptionRepository options, ChapterRepository chapters) {
        _context = context;
        _options = options;
        _chapters = chapters;
    }

    public Token LoginUser(Guid userId, string? issuedIp, string? issuedUserAgent) {
        var expires = DateTime.UtcNow.AddDays(GetTokenLifetimeDays());
        return _context.LoginUser(userId, expires, issuedIp, issuedUserAgent);
    }

    public void DeleteAllForUser(Guid userId) {
        _context.Tokens.Where(t => t.UserId == userId).Delete();
    }

    public void DeleteToken(Guid tokenId) {
        _context.Tokens.Where(t => t.Id == tokenId).Delete();
    }

    /// <summary>
    /// Returns the user's currently-valid Login tokens (expiry in the future, or no expiry set).
    /// Other token types (e.g. <see cref="TokenType.DonationMarker"/>) are not session tokens
    /// and are filtered out.
    /// </summary>
    public List<Token> GetActiveLoginTokensForUser(Guid userId) {
        var now = DateTime.UtcNow;
        return _context.Tokens
            .Where(t => t.UserId == userId
                && t.Type == TokenType.Login
                && (t.Expires == null || t.Expires > now))
            .OrderByDescending(t => t.IssuedAt)
            .ToList();
    }

    /// <summary>
    /// Deletes every Login token for the user EXCEPT the one matching <paramref name="exceptTokenId"/>.
    /// Used by the "revoke other sessions" flow to log the user out everywhere except the current device.
    /// </summary>
    public int DeleteOtherLoginTokensForUser(Guid userId, Guid exceptTokenId) {
        return _context.Tokens
            .Where(t => t.UserId == userId
                && t.Type == TokenType.Login
                && t.Id != exceptTokenId)
            .Delete();
    }

    /// <summary>
    /// Deletes a single token by id IF it belongs to <paramref name="ownerUserId"/>. Returns
    /// true when a row was removed; false when no such row exists (token already revoked, or
    /// belongs to a different user — both treated the same to avoid leaking ownership).
    /// </summary>
    public bool DeleteOwnedByUser(Guid tokenId, Guid ownerUserId) {
        return _context.Tokens
            .Where(t => t.Id == tokenId && t.UserId == ownerUserId)
            .Delete() > 0;
    }

    /// <summary>
    /// Looks up a login token by its raw content (Bearer token value).
    /// Returns the Token if valid, or null if not found or expired.
    /// On success, the token's expiry is extended by the configured lifetime
    /// (sliding window) when its ExtendType is Usage.
    /// </summary>
    public Token? ValidateLoginToken(string tokenContent) {
        if (string.IsNullOrEmpty(tokenContent))
            return null;

        var serverContent = TokenExtensions.HashTokenContent(tokenContent);

        var token = _context.Tokens
            .Where(t => t.Content == serverContent && t.Type == TokenType.Login)
            .FirstOrDefault();

        if (token == null)
            return null;

        if (token.Expires != null && token.Expires < DateTime.UtcNow)
            return null;

        ExtendExpiry(token);
        return token;
    }

    private void ExtendExpiry(Token token) {
        if (token.ExtendType != ExtendType.Usage)
            return;

        var newExpiry = DateTime.UtcNow.AddDays(GetTokenLifetimeDays());
        _context.Tokens.Where(t => t.Id == token.Id).Set(t => t.Expires, newExpiry).Update();
        token.Expires = newExpiry;
    }

    private int GetTokenLifetimeDays() {
        var value = _options.ResolveValue("auth.token.lifetime_days", null, _chapters);
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;
        return DefaultTokenLifetimeDays;
    }
}
