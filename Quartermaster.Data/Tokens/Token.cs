using System;
using System.Security.Cryptography;
using System.Text;
using LinqToDB;
using LinqToDB.Mapping;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Tokens;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Token {
    public const string TableName = "Tokens";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Content { get; set; } = "";
    public TokenType Type { get; set; }
    public DateTime? Expires { get; set; }
    public ExtendType ExtendType { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string? IssuedIp { get; set; }
    public string? IssuedUserAgent { get; set; }

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

public static class TokenExtensions {
    private const string PossibleTokenCharacters
        = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static string GenerateSimpleTokenContent(int length) => RandomNumberGenerator.GetString(PossibleTokenCharacters, length);

    internal static string HashTokenContent(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static Token LoginUser(this DbContext db, Guid userId, DateTime expires, string? issuedIp, string? issuedUserAgent) {
        var userContent = GenerateSimpleTokenContent(256);
        var token = new Token() {
            Content = HashTokenContent(userContent),
            Expires = expires,
            ExtendType = ExtendType.Usage,
            Id = Guid.NewGuid(),
            IssuedAt = DateTime.UtcNow,
            IssuedIp = issuedIp,
            IssuedUserAgent = issuedUserAgent,
            Type = TokenType.Login,
            UserId = userId
        };

        db.Insert(token);

        // Return value carries the user-visible random string, not the stored hash.
        token.Content = userContent;
        return token;
    }
}
