using InterpolatedSql.Dapper;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Quartermaster.Data.Tokens;

public class TokenRepository {
    private readonly DbContext _contenxt;

	public TokenRepository(DbContext context) {
		_contenxt = context;
	}

    public Token LoginUser(Guid userId) => _contenxt.LoginUser(userId, "");

	public bool CheckLoginToken(string tokenContent, Guid userId, string fingerprint)
		=> _contenxt.Tokens.CheckLoginToken(tokenContent, userId, fingerprint);

	public bool CheckToken(string tokenContent, Guid userId)
		=> _contenxt.Tokens.CheckSimpleToken(tokenContent, userId);

	/// <summary>
	/// Looks up a login token by its raw content (Bearer token value).
	/// Returns the Token if valid, or null if not found or expired.
	/// </summary>
	public Token? ValidateLoginToken(string tokenContent) {
		if (string.IsNullOrEmpty(tokenContent))
			return null;

		var serverContent = Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes($"{tokenContent};")));

		var token = _contenxt.Tokens
			.Where(t => t.Content == serverContent && t.Type == TokenType.Login)
			.FirstOrDefault();

		if (token == null)
			return null;

		if (token.Expires != null && token.Expires < DateTime.UtcNow)
			return null;

		return token;
	}
}