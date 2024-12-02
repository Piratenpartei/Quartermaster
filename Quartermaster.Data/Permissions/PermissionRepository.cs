using InterpolatedSql.Dapper;
using Quartermaster.Api;
using Quartermaster.Data.Abstract;
using System;
using System.Collections.Generic;

namespace Quartermaster.Data.Permissions;

public class PermissionRepository : RepositoryBase<Permission> {
	private readonly DbContext _context;

	internal PermissionRepository(DbContext context) {
		_context = context;
	}

	public Permission? GetByIdentifier(string identifier) {
		using var con = _context.GetConnection();
		return con.SqlBuilder(
			$"SELECT * FROM Permissions WHERE Identifier = {identifier}")
			.QuerySingleOrDefault<Permission>();
	}

	public Guid Create(Permission permission) {
		EnsureSetGuid(permission, p => p.Id);

		using var con = _context.GetConnection();
		con.SqlBuilder(
			$"INSERT INTO Permissions (Id, Identifier, DisplayName, Global) " +
			$"VALUES ({permission.Id}, {permission.Identifier}, {permission.DisplayName}, {permission.Global})")
			.Execute();

		return permission.Id;
	}

	public void SupplementDefaults() {
		AddIfNotExists(PermissionIdentifier.CreateUser, "Benutzer Erstellen", true);
		AddIfNotExists(PermissionIdentifier.CreateChapter, "Verband Erstellen", true);
	}

	private void AddIfNotExists(string identifier, string displayName, bool global) {
		if (GetByIdentifier(identifier) == null) {
            Create(new Permission {
				Identifier = identifier,
				DisplayName = displayName,
				Global = global,
				Id = Guid.NewGuid()
			});
        }
    }
}