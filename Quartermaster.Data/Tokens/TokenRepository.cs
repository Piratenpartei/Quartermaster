using LinqToDB;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

    public Token LoginUser(Guid userId) {
        var expires = DateTime.UtcNow.AddDays(GetTokenLifetimeDays());
        return _context.LoginUser(userId, "", expires);
    }

    public bool CheckLoginToken(string tokenContent, Guid userId, string fingerprint)
        => _context.Tokens.CheckLoginToken(tokenContent, userId, fingerprint);

    public bool CheckToken(string tokenContent, Guid userId)
        => _context.Tokens.CheckSimpleToken(tokenContent, userId);

    public void DeleteAllForUser(Guid userId) {
        _context.Tokens.Where(t => t.UserId == userId).Delete();
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

        var serverContent = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{tokenContent};")));

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
