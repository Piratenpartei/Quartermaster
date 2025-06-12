using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.Abstract;
using System;
using System.Linq;

namespace Quartermaster.Data.Permissions;

public class PermissionRepository {
	private readonly DbContext _context;

	public PermissionRepository(DbContext context) {
		_context = context;
	}

    public Permission? GetByIdentifier(string identifier)
		=> _context.Permissions.Where(p => p.Identifier == identifier).FirstOrDefault();

    public void Create(Permission permission) => _context.Insert(permission);

    public void SupplementDefaults() {
		AddIfNotExists(PermissionIdentifier.CreateUser, "Benutzer Erstellen", true);
		AddIfNotExists(PermissionIdentifier.CreateChapter, "Verband Erstellen", true);
	}

	private void AddIfNotExists(string identifier, string displayName, bool global) {
        if (GetByIdentifier(identifier) != null)
            return;

        Create(new Permission {
            Identifier = identifier,
            DisplayName = displayName,
            Global = global,
            Id = Guid.NewGuid()
        });
    }
}