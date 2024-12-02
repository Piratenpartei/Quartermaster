using InterpolatedSql.Dapper;
using System;
using System.Security.Cryptography;

namespace Quartermaster.Data.Tokens;

public class TokenRepository {
	private const string PossibleTokenCharacters
		= "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly DbContext _contenxt;

	public TokenRepository(DbContext context) {
		_contenxt = context;
	}

	public Token LoginUser(Guid userId) {
		var content = RandomNumberGenerator.GetString(PossibleTokenCharacters, 32);
		var token = new Token() {
			Content = content,
			ExtendType = ExtendType.Usage,
			Id = Guid.NewGuid(),
			SecurityScope = TokenSecurityScope.None,
			Type = TokenType.Login,
			UserId = userId
		};

		using var con = _contenxt.GetConnection();
		con.SqlBuilder($"INSERT INTO Tokens (Id, UserId, Content, Type, Expires, ExtendType, SecurityScope) " +
			$"VALUES ({token.Id}, {token.UserId}, {token.Content}, {token.Type}, " +
			$"{token.Expires}, {token.ExtendType}, {token.SecurityScope})").Execute();
		return token;
	}

	public bool CheckToken(string tokenContent, Guid userId) {
		if (string.IsNullOrEmpty(tokenContent) || userId == Guid.Empty)
			return false;

		using var con = _contenxt.GetConnection();
		var token = con.SqlBuilder($"SELECT * FROM Tokens WHERE UserId = {userId} AND Content = {tokenContent}")
			.QuerySingleOrDefault<Token>();

		if (token == null || token.Expires < DateTime.UtcNow)
			return false;

		//TODO: Add SecurityScope checks

		//TODO: Grab expiration extension time from some Setting instead of blindly adding a day
		if (token.Expires != null && token.ExtendType == ExtendType.Usage) {
			con.SqlBuilder($"UPDATE Tokens SET Expires = {token.Expires.Value.AddDays(1)}" +
				$"WHERE Id = {token.Id}");
		}

		return true;
	}
}