using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Permissions;

public class PermissionRepository {
	private readonly DbContext _context;

	public PermissionRepository(DbContext context) {
		_context = context;
	}

    public List<Permission> GetAll()
		=> _context.Permissions.OrderBy(p => p.Identifier).ToList();

    public Permission? GetByIdentifier(string identifier)
		=> _context.Permissions.Where(p => p.Identifier == identifier).FirstOrDefault();

    public void Create(Permission permission) => _context.Insert(permission);

    public void SupplementDefaults() {
        // Global permissions
		AddIfNotExists(PermissionIdentifier.CreateUser, "Benutzer Erstellen", true);
		AddIfNotExists(PermissionIdentifier.ViewUsers, "Benutzer anzeigen", true);
		AddIfNotExists(PermissionIdentifier.DeleteUsers, "Benutzer löschen", true);
		AddIfNotExists(PermissionIdentifier.CreateChapter, "Verband Erstellen", true);
        AddIfNotExists(PermissionIdentifier.ViewOptions, "Einstellungen anzeigen", true);
        AddIfNotExists(PermissionIdentifier.EditOptions, "Einstellungen bearbeiten", true);
        AddIfNotExists(PermissionIdentifier.ViewAudit, "Audit-Log anzeigen", true);
        AddIfNotExists(PermissionIdentifier.ViewEmailLogs, "E-Mail-Log anzeigen", true);
        AddIfNotExists(PermissionIdentifier.TriggerMemberImport, "Mitgliederimport auslösen", true);
        AddIfNotExists(PermissionIdentifier.ViewAllMembers, "Alle Mitglieder anzeigen", true);
        AddIfNotExists(PermissionIdentifier.ManageRoles, "Rollen verwalten", true);
        AddIfNotExists(PermissionIdentifier.SystemVote, "Systemweites Stimmrecht", true);

        // Chapter-scoped permissions
        AddIfNotExists(PermissionIdentifier.ViewApplications, "Mitgliedsanträge Einsehen", false);
        AddIfNotExists(PermissionIdentifier.ProcessApplications, "Mitgliedsanträge Bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.ViewDueSelections, "Beitragseinstufungen Einsehen", false);
        AddIfNotExists(PermissionIdentifier.ProcessDueSelections, "Beitragseinstufungen Bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.ViewEvents, "Events anzeigen", false);
        AddIfNotExists(PermissionIdentifier.CreateEvents, "Events erstellen", false);
        AddIfNotExists(PermissionIdentifier.EditEvents, "Events bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.DeleteEvents, "Events löschen/archivieren", false);
        AddIfNotExists(PermissionIdentifier.ViewMotions, "Anträge anzeigen", false);
        AddIfNotExists(PermissionIdentifier.EditMotions, "Anträge bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.VoteMotions, "Abstimmen", false);
        AddIfNotExists(PermissionIdentifier.VoteDelegateMotions, "Stellvertretend Abstimmen", false);
        AddIfNotExists(PermissionIdentifier.ViewMembers, "Mitglieder anzeigen", false);
        AddIfNotExists(PermissionIdentifier.EditMembers, "Mitglieder bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.ViewOfficers, "Vorstand anzeigen", false);
        AddIfNotExists(PermissionIdentifier.EditOfficers, "Vorstand bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.ViewTemplates, "Vorlagen anzeigen", false);
        AddIfNotExists(PermissionIdentifier.EditTemplates, "Vorlagen bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.ViewMeetings, "Sitzungen anzeigen", false);
        AddIfNotExists(PermissionIdentifier.CreateMeetings, "Sitzungen erstellen", false);
        AddIfNotExists(PermissionIdentifier.EditMeetings, "Sitzungen bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.DeleteMeetings, "Sitzungen löschen/archivieren", false);
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