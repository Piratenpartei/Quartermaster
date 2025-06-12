using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LinqToDB;
using LinqToDB.Mapping;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Tokens;

[Table("Tokens", IsColumnAttributeRequired = false)]
public class Token {
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Content { get; set; } = "";
    public TokenType Type { get; set; }
    public DateTime? Expires { get; set; }
    public ExtendType ExtendType { get; set; }
    public TokenSecurityScope SecurityScope { get; set; }

    [Association(ThisKey = nameof(UserId), OtherKey = nameof(User.Id))]
    public User? User { get; set; }
}

public enum TokenType {
    Login,
    DonationMarker
}

public enum ExtendType {
    /// <summary> Specifies a Token cannot be extended at all. </summary>
    None,
    /// <summary> Specifies a Token can be extended without renewed authentication. </summary>
    Usage,
    /// <summary> Specifies a Token can be extended but the user must re-enter their Password. </summary>
    Password
}

public enum TokenSecurityScope {
    None,
    IP,
    BrowserFingerprint
}

public static class TokenExtensions {
    private const string PossibleTokenCharacters
        = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static string GenerateSimpleTokenContent(int length) => RandomNumberGenerator.GetString(PossibleTokenCharacters, length);

    private static string GenerateLoginTokenIP(string baseContent, string ip) => Hash($"{baseContent};{ip}");

    private static string Hash(string str) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(str)));

    public static Token LoginUser(this DbContext db, Guid userId, string fingerprint) {
        var userContent = GenerateSimpleTokenContent(256);
        var serverContent = GenerateLoginTokenIP(userContent, fingerprint);
        var token = new Token() {
            Content = serverContent,
            ExtendType = ExtendType.Usage,
            Id = Guid.NewGuid(),
            SecurityScope = TokenSecurityScope.BrowserFingerprint,
            Type = TokenType.Login,
            UserId = userId
        };

        // Store Token with the server content (including IP) in the DB
        db.Insert(token);

        // But send the user only the random string
        token.Content = userContent;
        return token;
    }

    public static bool CheckLoginToken(this ITable<Token> tokenTable, string tokenContent, Guid userId, string? fingerprint) {
        if (string.IsNullOrEmpty(tokenContent) || userId == Guid.Empty)
            return false;

        var serverContent = GenerateLoginTokenIP(tokenContent, fingerprint ?? "");
        var token = tokenTable.Where(t => t.UserId == userId && t.Content == serverContent).FirstOrDefault();

        if (token == null)
            return false;

        if (token.Expires != null && token.Expires < DateTime.UtcNow)
            return false;

        UpdateTokenExpiration(tokenTable, token);
        return true;
    }

    public static bool CheckSimpleToken(this ITable<Token> tokenTable, string tokenContent, Guid userId) {
        if (string.IsNullOrEmpty(tokenContent) || userId == Guid.Empty)
            return false;

        var hash = Hash(tokenContent);
        var token = tokenTable.Where(t => t.UserId == userId && t.Content == hash).FirstOrDefault();
        if (token == null || token.Expires < DateTime.UtcNow)
            return false;

        UpdateTokenExpiration(tokenTable, token);
        return true;
    }

    private static void UpdateTokenExpiration(ITable<Token> tokenTable, Token token) {
        if (token.Expires == null || token.ExtendType != ExtendType.Usage)
            return;

        //TODO: Grab expiration extension time from some Setting instead of blindly adding a day
        tokenTable.Where(t => t.Id == token.Id).Set(t => t.Expires, token.Expires.Value.AddDays(1)).Update();
    }
}