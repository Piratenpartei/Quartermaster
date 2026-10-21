using LinqToDB;
using Quartermaster.Api;
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
        AddOrRename(PermissionIdentifier.CreateUser, "Benutzer erstellen", true);
        AddOrRename(PermissionIdentifier.ViewUsers, "Benutzer anzeigen", true);
        AddOrRename(PermissionIdentifier.DeleteUsers, "Benutzer löschen", true);
        AddOrRename(PermissionIdentifier.CreateChapter, "Gliederung erstellen", true);
        AddOrRename(PermissionIdentifier.EditChapter, "Gliederung bearbeiten", true);
        AddOrRename(PermissionIdentifier.DeleteChapter, "Gliederung löschen", true);
        AddOrRename(PermissionIdentifier.ArchiveChapter, "Gliederung archivieren/wiederherstellen", true);
        AddOrRename(PermissionIdentifier.ViewOptions, "Einstellungen anzeigen", true);
        AddOrRename(PermissionIdentifier.EditOptions, "Einstellungen bearbeiten", true);
        AddOrRename(PermissionIdentifier.ViewOptionSecrets, "Einstellungen: Geheimnisse im Klartext anzeigen", true);
        AddOrRename(PermissionIdentifier.ViewAudit, "Audit-Log anzeigen", true);
        AddOrRename(PermissionIdentifier.ViewNotificationLogs, "Benachrichtigungs-Log anzeigen", true);
        AddOrRename(PermissionIdentifier.TriggerMemberImport, "Mitgliederimport auslösen", true);
        AddOrRename(PermissionIdentifier.ViewAllMembers, "Alle Mitglieder anzeigen", true);
        AddOrRename(PermissionIdentifier.ManageRoles, "Rollen verwalten", true);
        AddOrRename(PermissionIdentifier.EditPdfTemplates, "PDF-Vorlagen bearbeiten", true);
        AddOrRename(PermissionIdentifier.ReviewNewsPosts, "Social-Media-Vorschläge prüfen und freigeben", true);
        AddOrRename(PermissionIdentifier.ManageNewsPosts, "Social-Media-Quellen verwalten", true);
        AddOrRename(PermissionIdentifier.AssignPermissionsGlobally, "Berechtigungen global vergeben", true);
        AddOrRename(PermissionIdentifier.ManageAccreditation, "Akkreditierung durchführen", true);
        AddOrRename(PermissionIdentifier.SystemVote, "Systemweites Stimmrecht", true);
        AddOrRename(PermissionIdentifier.LinkApplicationDivision, "Mitgliedsanträge: Gliederung zuordnen", true);

        AddOrRename(PermissionIdentifier.ViewApplications, "Mitgliedsanträge einsehen", false);
        AddOrRename(PermissionIdentifier.ProcessApplications, "Mitgliedsanträge bearbeiten", false);
        AddOrRename(PermissionIdentifier.ViewDueSelections, "Beitragseinstufungen einsehen", false);
        AddOrRename(PermissionIdentifier.ProcessDueSelections, "Beitragseinstufungen bearbeiten", false);
        AddOrRename(PermissionIdentifier.ViewEvents, "Veranstaltungen anzeigen", false);
        AddOrRename(PermissionIdentifier.CreateEvents, "Veranstaltungen erstellen", false);
        AddOrRename(PermissionIdentifier.EditEvents, "Veranstaltungen bearbeiten", false);
        AddOrRename(PermissionIdentifier.DeleteEvents, "Veranstaltungen löschen/archivieren", false);
        AddOrRename(PermissionIdentifier.ViewMotions, "Anträge anzeigen", false);
        AddOrRename(PermissionIdentifier.EditMotions, "Anträge bearbeiten", false);
        AddOrRename(PermissionIdentifier.VoteMotions, "Abstimmen", false);
        AddOrRename(PermissionIdentifier.VoteDelegateMotions, "Stellvertretend abstimmen", false);
        AddOrRename(PermissionIdentifier.ViewMembers, "Mitglieder anzeigen", false);
        AddOrRename(PermissionIdentifier.EditMembers, "Mitglieder bearbeiten", false);
        AddOrRename(PermissionIdentifier.ViewOfficers, "Vorstand anzeigen", false);
        AddOrRename(PermissionIdentifier.EditOfficers, "Vorstand bearbeiten", false);
        AddOrRename(PermissionIdentifier.ViewTemplates, "Vorlagen anzeigen", false);
        AddOrRename(PermissionIdentifier.EditTemplates, "Vorlagen bearbeiten", false);
        AddOrRename(PermissionIdentifier.ViewMeetings, "Sitzungen anzeigen", false);
        AddOrRename(PermissionIdentifier.CreateMeetings, "Sitzungen erstellen", false);
        AddOrRename(PermissionIdentifier.EditMeetings, "Sitzungen bearbeiten", false);
        AddOrRename(PermissionIdentifier.DeleteMeetings, "Sitzungen löschen/archivieren", false);
        AddOrRename(PermissionIdentifier.ManageChapterMail, "Mailings verwalten", false);
        AddOrRename(PermissionIdentifier.ManageAssemblyMotions, "Antragskommission (Versammlungsanträge)", false);
        AddOrRename(PermissionIdentifier.AssemblyChair, "Versammlungsleitung", false);
        AddOrRename(PermissionIdentifier.AssemblyView, "Versammlung mitlesen", false);
        AddOrRename(PermissionIdentifier.EditTravelExpenseRules, "Reisekostenordnung bearbeiten", false);
        AddOrRename(PermissionIdentifier.ManageSupportSignatures, "UU-Assistent: Sammlungen verwalten", false);
        AddOrRename(PermissionIdentifier.UnlimitedReceipts, "Belege: unbegrenzt hochladen", true);
    }

    /// <summary>Adds the permission, or renames an existing one whose name differs, so text fixes reach installations that already have it.</summary>
    private void AddOrRename(string identifier, string displayName, bool global) {
        var existing = GetByIdentifier(identifier);
        if (existing != null) {
            if (existing.DisplayName != displayName) {
                _context.Permissions
                    .Where(p => p.Id == existing.Id)
                    .Set(p => p.DisplayName, displayName)
                    .Update();
            }
            return;
        }

        Create(new Permission {
            Identifier = identifier,
            DisplayName = displayName,
            Global = global,
            Id = Guid.NewGuid()
        });
    }
}
