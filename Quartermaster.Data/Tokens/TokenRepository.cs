using InterpolatedSql.Dapper;
using System;
using System.Security.Cryptography;

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
}