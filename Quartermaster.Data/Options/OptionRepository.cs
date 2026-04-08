using LinqToDB;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Options;

public class OptionRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public OptionRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public List<OptionDefinition> GetAllDefinitions()
        => _context.OptionDefinitions.OrderBy(d => d.Identifier).ToList();

    public OptionDefinition? GetDefinition(string identifier)
        => _context.OptionDefinitions.Where(d => d.Identifier == identifier).FirstOrDefault();

    public void CreateDefinition(OptionDefinition def) => _context.Insert(def);

    public SystemOption? GetGlobalValue(string identifier)
        => _context.SystemOptions
            .Where(o => o.Identifier == identifier && o.ChapterId == null)
            .FirstOrDefault();

    public SystemOption? GetChapterValue(string identifier, Guid chapterId)
        => _context.SystemOptions
            .Where(o => o.Identifier == identifier && o.ChapterId == chapterId)
            .FirstOrDefault();

    public List<SystemOption> GetAllValues()
        => _context.SystemOptions.ToList();

    public string? ResolveValue(string identifier, Guid? chapterId, ChapterRepository chapterRepo) {
        if (chapterId.HasValue) {
            var chain = chapterRepo.GetAncestorChain(chapterId.Value);
            foreach (var chapter in chain) {
                var chapterValue = GetChapterValue(identifier, chapter.Id);
                if (chapterValue != null)
                    return chapterValue.Value;
            }
        }

        var global = GetGlobalValue(identifier);
        return global?.Value;
    }

    public void SetValue(string identifier, Guid? chapterId, string value) {
        var existing = chapterId.HasValue
            ? GetChapterValue(identifier, chapterId.Value)
            : GetGlobalValue(identifier);

        if (existing != null) {
            var oldValue = existing.Value;
            _context.SystemOptions
                .Where(o => o.Id == existing.Id)
                .Set(o => o.Value, value)
                .Update();
            _auditLog.LogFieldChange("SystemOption", existing.Id, identifier, oldValue, value);
        } else {
            var option = new SystemOption {
                Identifier = identifier,
                Value = value,
                ChapterId = chapterId
            };
            _context.Insert(option);
            _auditLog.LogCreated("SystemOption", option.Id);
            _auditLog.LogFieldChange("SystemOption", option.Id, identifier, null, value);
        }
    }

    public void SupplementDefaults() {
        AddDefinitionIfNotExists("templates.membershipapplication.approved.email",
            "E-Mail: Mitgliedsantrag genehmigt",
            OptionDataType.Template, true,
            "MembershipApplicationDetailDTO,ChapterDTO",
            "Hallo **{{ application.FirstName }}**,\n\ndein Mitgliedsantrag bei der **{{ chapter.Name }}** wurde genehmigt.\n\nWillkommen an Bord!\n");

        AddDefinitionIfNotExists("templates.membershipapplication.rejected.email",
            "E-Mail: Mitgliedsantrag abgelehnt",
            OptionDataType.Template, true,
            "MembershipApplicationDetailDTO,ChapterDTO",
            "Hallo **{{ application.FirstName }}**,\n\nleider wurde dein Mitgliedsantrag bei der **{{ chapter.Name }}** abgelehnt.\n");

        AddDefinitionIfNotExists("templates.dueselection.approved.email",
            "E-Mail: Beitragsminderung genehmigt",
            OptionDataType.Template, true,
            "DueSelectionDetailDTO,ChapterDTO",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde genehmigt.\n");

        AddDefinitionIfNotExists("templates.dueselection.rejected.email",
            "E-Mail: Beitragsminderung abgelehnt",
            OptionDataType.Template, true,
            "DueSelectionDetailDTO,ChapterDTO",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde leider abgelehnt.\n");

        AddDefinitionIfNotExists("general.chaptername.display",
            "Anzeigename der Gliederung",
            OptionDataType.String, true, "", "");

        AddDefinitionIfNotExists("general.contact.email",
            "Kontakt E-Mail Adresse",
            OptionDataType.String, true, "", "");

        AddDefinitionIfNotExists("member_import.file_path",
            "Mitgliederimport: Dateipfad",
            OptionDataType.String, false, "", "",
            description: "Absoluter Pfad zur CSV-Exportdatei, z.B. /data/system_export.csv");

        AddDefinitionIfNotExists("member_import.polling_interval_minutes",
            "Mitgliederimport: Abfrageintervall",
            OptionDataType.Number, false, "", "10",
            description: "Intervall in Minuten, in dem die Importdatei auf Änderungen geprüft wird.");

        AddDefinitionIfNotExists("auth.saml.endpoint",
            "SAML: Endpunkt-URL",
            OptionDataType.String, false, "", "",
            description: "SSO-Endpunkt des Identity Providers, z.B. https://keycloak.example.com/realms/master/protocol/saml");

        AddDefinitionIfNotExists("auth.saml.client_id",
            "SAML: Client-ID",
            OptionDataType.String, false, "", "",
            description: "Die Entity-ID / Client-ID des SAML-Clients im Identity Provider.");

        AddDefinitionIfNotExists("auth.saml.certificate",
            "SAML: Zertifikat",
            OptionDataType.String, false, "", "",
            description: "Base64-kodiertes Signaturzertifikat des Identity Providers (ohne BEGIN/END CERTIFICATE Header). Zu finden unter Realm Settings > Keys > RS256 > Certificate.");

        AddDefinitionIfNotExists("auth.saml.button_text",
            "SAML: Login-Button Text",
            OptionDataType.String, false, "", "SSO Login",
            description: "Text, der auf dem SSO-Login-Button auf der Anmeldeseite angezeigt wird.");

        AddDefinitionIfNotExists("auth.sso.support_contact",
            "SSO: Support-Kontakt",
            OptionDataType.String, false, "", "",
            description: "Kontaktinformation, die angezeigt wird, wenn eine SSO-Anmeldung fehlschlägt (z.B. E-Mail-Adresse oder URL).");

        AddDefinitionIfNotExists("auth.oidc.authority",
            "OIDC: Authority-URL",
            OptionDataType.String, false, "", "",
            description: "OpenID Connect Authority-URL, z.B. https://keycloak.example.com/realms/master");

        AddDefinitionIfNotExists("auth.oidc.client_id",
            "OIDC: Client-ID",
            OptionDataType.String, false, "", "",
            description: "Die Client-ID des OIDC-Clients im Identity Provider.");

        AddDefinitionIfNotExists("auth.oidc.client_secret",
            "OIDC: Client-Secret",
            OptionDataType.String, false, "", "",
            description: "Das Client-Secret des OIDC-Clients. Zu finden unter Clients > Credentials im Identity Provider.");

        AddDefinitionIfNotExists("auth.oidc.button_text",
            "OIDC: Login-Button Text",
            OptionDataType.String, false, "", "OpenID Login",
            description: "Text, der auf dem OpenID-Login-Button auf der Anmeldeseite angezeigt wird.");

        AddDefinitionIfNotExists("general.error.contact",
            "Fehlerkontakt",
            OptionDataType.String, true, "", "Bei Problemen wende dich bitte an den Vorstand deiner Gliederung.",
            description: "Wird bei Fehlern in der Anwendung als Kontakthinweis angezeigt.");

        AddDefinitionIfNotExists("general.error.show_details",
            "Fehlerdetails anzeigen",
            OptionDataType.String, false, "", "false",
            description: "Wenn 'true', werden technische Fehlerdetails in der UI angezeigt. Nur für Administratoren empfohlen.");

        AddDefinitionIfNotExists("email.smtp.host",
            "SMTP: Server",
            OptionDataType.String, false, "", "",
            description: "Hostname des SMTP-Servers, z.B. smtp.example.com");

        AddDefinitionIfNotExists("email.smtp.port",
            "SMTP: Port",
            OptionDataType.Number, false, "", "587",
            description: "Port des SMTP-Servers. Standard: 587 (STARTTLS) oder 465 (SSL).");

        AddDefinitionIfNotExists("email.smtp.username",
            "SMTP: Benutzername",
            OptionDataType.String, false, "", "",
            description: "Benutzername für die SMTP-Authentifizierung.");

        AddDefinitionIfNotExists("email.smtp.password",
            "SMTP: Passwort",
            OptionDataType.String, false, "", "",
            description: "Passwort für die SMTP-Authentifizierung.");

        AddDefinitionIfNotExists("email.smtp.sender_address",
            "SMTP: Absenderadresse",
            OptionDataType.String, false, "", "",
            description: "E-Mail-Adresse, die als Absender verwendet wird.");

        AddDefinitionIfNotExists("email.smtp.sender_name",
            "SMTP: Absendername",
            OptionDataType.String, false, "", "Quartermaster",
            description: "Name, der als Absender angezeigt wird.");

        AddDefinitionIfNotExists("email.smtp.use_ssl",
            "SMTP: SSL verwenden",
            OptionDataType.String, false, "", "true",
            description: "Wenn 'true', wird eine verschlüsselte Verbindung zum SMTP-Server verwendet.");

        AddDefinitionIfNotExists("email.smtp.batch_size",
            "SMTP: Batch-Größe",
            OptionDataType.Number, false, "", "50",
            description: "Maximale Anzahl an E-Mails, die pro SMTP-Verbindung gesendet werden. Der Server wartet auf eine Nachricht, nimmt dann bis zu dieser Anzahl sofort verfügbarer weiterer Nachrichten mit, und sendet alle über eine einzige Verbindung. Standard: 50.");

        AddDefinitionIfNotExists("auth.lockout.max_attempts",
            "Login-Sperre: Max. Fehlversuche",
            OptionDataType.Number, false, "", "5",
            description: "Anzahl fehlgeschlagener Login-Versuche (pro IP+Benutzer) innerhalb der Sperrdauer, bevor eine Sperre greift. Standard: 5.");

        AddDefinitionIfNotExists("auth.lockout.duration_minutes",
            "Login-Sperre: Sperrdauer (Minuten)",
            OptionDataType.Number, false, "", "15",
            description: "Zeitfenster und Sperrdauer in Minuten: Fehlversuche werden in diesem Fenster gezählt, und die Sperre gilt so lange nach dem letzten Fehlversuch. Standard: 15.");

        AddDefinitionIfNotExists("meetings.protocol.archive_dir",
            "Sitzungen: Protokoll-Archivpfad",
            OptionDataType.String, false, "", "",
            description: "Absoluter Pfad, unter dem archivierte Sitzungsprotokolle als PDF gespeichert werden. Standard: ./data/protocols");

        AddDefinitionIfNotExists("meetings.motion_notes_template",
            "Sitzungen: Antragsnotiz-Vorlage",
            OptionDataType.Template, true,
            "MotionDTO",
            "**Antrag:** {{ motion.Title }}\n\n**Antragsteller:** {{ motion.AuthorName }}\n\n{{ motion.Text }}\n\n---\n\n**Diskussion:**\n",
            description: "Fluid-Template für vorausgefüllte Notizen bei Antrag-Tagesordnungspunkten. Verfügbare Variablen: motion.Title, motion.AuthorName, motion.AuthorEMail, motion.Text");
    }

    private void AddDefinitionIfNotExists(string identifier, string friendlyName,
        OptionDataType dataType, bool isOverridable, string templateModels, string defaultValue,
        string description = "") {

        var existing = GetDefinition(identifier);
        if (existing != null) {
            if (existing.FriendlyName != friendlyName || (!string.IsNullOrEmpty(description) && existing.Description != description)) {
                _context.OptionDefinitions
                    .Where(d => d.Id == existing.Id)
                    .Set(d => d.FriendlyName, friendlyName)
                    .Set(d => d.Description, description)
                    .Update();
            }
            return;
        }

        CreateDefinition(new OptionDefinition {
            Identifier = identifier,
            FriendlyName = friendlyName,
            Description = description,
            DataType = dataType,
            IsOverridable = isOverridable,
            TemplateModels = templateModels
        });

        if (!string.IsNullOrEmpty(defaultValue))
            SetValue(identifier, null, defaultValue);
    }
}
