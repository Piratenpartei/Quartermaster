using LinqToDB;
using Quartermaster.Api.Options;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Receipts;
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

    /// <summary>The global value and every chapter override of one option.</summary>
    public List<SystemOption> GetValues(string identifier)
        => _context.SystemOptions.Where(o => o.Identifier == identifier).ToList();

    public string? ResolveValue(string identifier, Guid? chapterId, ChapterRepository chapterRepo) {
        if (chapterId.HasValue) {
            var chain = chapterRepo.GetAncestorChainIds(chapterId.Value);
            if (chain.Count > 0) {
                var valuesByChapter = _context.SystemOptions
                    .Where(o => o.Identifier == identifier
                        && o.ChapterId != null
                        && chain.Contains(o.ChapterId.Value))
                    .ToDictionary(o => o.ChapterId!.Value, o => o.Value);
                foreach (var id in chain) {
                    if (valuesByChapter.TryGetValue(id, out var value))
                        return value;
                }
            }
        }

        var global = GetGlobalValue(identifier);
        return global?.Value;
    }

    public void SetValue(string identifier, Guid? chapterId, string value) {
        var existing = chapterId.HasValue
            ? GetChapterValue(identifier, chapterId.Value)
            : GetGlobalValue(identifier);
        var definition = GetDefinition(identifier);
        var redact = definition?.IsSecret == true;

        using var tx = _context.BeginTransaction();
        if (existing != null) {
            var oldValue = existing.Value;
            _context.SystemOptions
                .Where(o => o.Id == existing.Id)
                .Set(o => o.Value, value)
                .Update();
            _auditLog.LogFieldChange("SystemOption", existing.Id, identifier,
                redact ? RedactedAuditValue(oldValue) : oldValue,
                redact ? RedactedAuditValue(value) : value);
        } else {
            var option = new SystemOption {
                Identifier = identifier,
                Value = value,
                ChapterId = chapterId
            };
            _context.Insert(option);
            _auditLog.LogCreated("SystemOption", option.Id);
            _auditLog.LogFieldChange("SystemOption", option.Id, identifier, null,
                redact ? RedactedAuditValue(value) : value);
        }
        tx.Commit();
    }

    private const string SecretMask = "••••••";
    private static string? RedactedAuditValue(string? raw) => string.IsNullOrEmpty(raw) ? raw : SecretMask;

    /// <summary>Removes a chapter override so the chapter inherits again; false when there was none.</summary>
    public bool DeleteChapterValue(string identifier, Guid chapterId) {
        var existing = GetChapterValue(identifier, chapterId);
        if (existing == null)
            return false;

        using var tx = _context.BeginTransaction();
        _context.SystemOptions
            .Where(o => o.Id == existing.Id)
            .Delete();
        _auditLog.LogDeleted("SystemOption", existing.Id);
        tx.Commit();
        return true;
    }

    public void SupplementDefaults() {
        AddDefinitionIfNotExists(PublicFormOptions.MembershipApplication,
            "Öffentliche Formulare: Mitgliedsantrag",
            OptionDataType.String, false, "", "false",
            description: "Wenn 'true', ist der Mitgliedsantrag öffentlich erreichbar und im Menü sichtbar.");

        AddDefinitionIfNotExists(PublicFormOptions.DueSelection,
            "Öffentliche Formulare: Beitragseinstufung",
            OptionDataType.String, false, "", "false",
            description: "Wenn 'true', ist die Beitragseinstufung öffentlich erreichbar und im Menü sichtbar.");

        AddDefinitionIfNotExists(PublicFormOptions.Motion,
            "Öffentliche Formulare: Antrag einreichen",
            OptionDataType.String, true, "", "false",
            description: "Wenn 'true', können Anträge an diese Gliederung über das öffentliche Formular eingereicht werden. Ein Wert an einer Gliederung gilt auch für ihre Untergliederungen, solange diese keinen eigenen setzen. Ist keine Gliederung freigeschaltet, verschwindet der Menüpunkt.");

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
            description: "Base64-kodiertes Signaturzertifikat des Identity Providers (ohne BEGIN/END CERTIFICATE Header). Zu finden unter Realm Settings > Keys > RS256 > Certificate.",
            isSecret: true);

        AddDefinitionIfNotExists("auth.saml.button_text",
            "SAML: Login-Button Text",
            OptionDataType.String, false, "", "SSO Login",
            description: "Text, der auf dem SSO-Login-Button auf der Anmeldeseite angezeigt wird.");

        AddDefinitionIfNotExists("auth.saml.expected_audience",
            "SAML: Erwarteter Audience-Wert",
            OptionDataType.String, false, "", "",
            description: "Der SP-Entity-ID-Wert, den die SAML-Assertion im <Audience>-Element tragen muss. Wenn leer, wird die Audience-Prüfung übersprungen (nicht empfohlen für Produktion).");

        AddDefinitionIfNotExists("auth.saml.expected_destination",
            "SAML: Erwartete Destination-URL",
            OptionDataType.String, false, "", "",
            description: "Die URL, an die der IdP die SAML-Antwort senden soll (z.B. https://quartermaster.example.de/api/users/SamlConsume). Muss mit dem <Destination>-Attribut der Antwort übereinstimmen. Wenn leer, wird die Destination-Prüfung übersprungen (nicht empfohlen für Produktion).");

        AddDefinitionIfNotExists("auth.saml.member_number_attribute",
            "SAML: Attribut für Mitgliedsnummer",
            OptionDataType.String, false, "", "member_number",
            description: "Name des SAML-Attributs, das die Mitgliedsnummer enthält. Benutzer werden über die Mitgliedsnummer mit Mitgliedern verknüpft; fehlt ein Mitglied, wird es mit Vorname, Nachname, E-Mail und Mitgliedsnummer angelegt.");

        AddDefinitionIfNotExists("auth.saml.department_attribute",
            "SAML: Attribut für Gliederung",
            OptionDataType.String, false, "", "ekklesia_department",
            description: "Name des SAML-Attributs mit der Keycloak-Gliederung des Kontos. Passt der Wert zur Keycloak-Gliederung einer Gliederung, wird das Mitglied beim Login dieser Gliederung zugeordnet, sofern es nicht schon einer Gliederung darunter angehört.");

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
            description: "Das Client-Secret des OIDC-Clients. Zu finden unter Clients > Credentials im Identity Provider.",
            isSecret: true);

        AddDefinitionIfNotExists("auth.oidc.member_number_claim",
            "OIDC: Claim für Mitgliedsnummer",
            OptionDataType.String, false, "", "member_number",
            description: "Name des Claims im ID-Token, der die Mitgliedsnummer enthält. Benutzer werden über die Mitgliedsnummer mit Mitgliedern verknüpft; fehlt ein Mitglied, wird es mit Vorname, Nachname, E-Mail und Mitgliedsnummer angelegt.");

        AddDefinitionIfNotExists("auth.oidc.department_claim",
            "OIDC: Claim für Gliederung",
            OptionDataType.String, false, "", "ekklesia_department",
            description: "Name des Claims im ID-Token mit der Keycloak-Gliederung des Kontos. Passt der Wert zur Keycloak-Gliederung einer Gliederung, wird das Mitglied beim Login dieser Gliederung zugeordnet, sofern es nicht schon einer Gliederung darunter angehört.");

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
            description: "Passwort für die SMTP-Authentifizierung.",
            isSecret: true);

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

        AddDefinitionIfNotExists("email.imap.host",
            "IMAP: Server",
            OptionDataType.String, false, "", "",
            description: "Hostname des IMAP-Servers für eingehende Lesebestätigungen.");

        AddDefinitionIfNotExists("email.imap.port",
            "IMAP: Port",
            OptionDataType.Number, false, "", "993",
            description: "Port des IMAP-Servers. Standard: 993 (SSL) oder 143 (STARTTLS).");

        AddDefinitionIfNotExists("email.imap.use_ssl",
            "IMAP: SSL verwenden",
            OptionDataType.String, false, "", "true",
            description: "Wenn 'true', wird SSL/TLS direkt beim Verbindungsaufbau verwendet (Port 993). Bei 'false' STARTTLS auf Port 143.");

        AddDefinitionIfNotExists("email.imap.username",
            "IMAP: Benutzername",
            OptionDataType.String, false, "", "",
            description: "Benutzername für die IMAP-Authentifizierung.");

        AddDefinitionIfNotExists("email.imap.password",
            "IMAP: Passwort",
            OptionDataType.String, false, "", "",
            description: "Passwort für die IMAP-Authentifizierung.",
            isSecret: true);

        AddDefinitionIfNotExists("email.imap.mailbox",
            "IMAP: Postfach",
            OptionDataType.String, false, "", "INBOX",
            description: "Postfach, in dem nach eingehenden Lesebestätigungen gesucht wird. Standard: INBOX.");

        AddDefinitionIfNotExists("email.imap.poll_interval_minutes",
            "IMAP: Abrufintervall (Minuten)",
            OptionDataType.Number, false, "", "15",
            description: "Wie oft das Postfach nach neuen Lesebestätigungen geprüft wird. Standard: 15 Minuten.");

        AddDefinitionIfNotExists("email.mdn.request_default",
            "MDN: Lesebestätigung anfordern (Standard)",
            OptionDataType.String, false, "", "true",
            description: "Wenn 'true', wird für jede ausgehende E-Mail standardmäßig eine Lesebestätigung angefordert. Empfänger können diese Anforderung in ihrem Mailprogramm ablehnen.");

        AddDefinitionIfNotExists("email.mdn.receipt_address",
            "MDN: Empfangsadresse",
            OptionDataType.String, false, "", "",
            description: "E-Mail-Adresse, an die Lesebestätigungen geschickt werden. Bleibt das Feld leer, wird die SMTP-Absenderadresse verwendet.");

        AddDefinitionIfNotExists("messaging.telegram.bot_token",
            "Telegram: Bot-Token",
            OptionDataType.String, false, "", "",
            description: "Telegram Bot API Token (von @BotFather). Wenn leer, ist der Telegram-Kanal deaktiviert.",
            isSecret: true);

        AddDefinitionIfNotExists("system.public_base_url",
            "System: Öffentliche Basis-URL",
            OptionDataType.String, false, "", "",
            description: "Vollständige öffentliche URL der Anwendung ohne abschließenden Schrägstrich, z. B. https://quartermaster.example.de. Wird in Benachrichtigungen verwendet, um Direktlinks (z. B. zu Anträgen) aufzubauen. Leer ⇒ Links werden nicht generiert.");

        AddDefinitionIfNotExists("system.app_name",
            "System: Anzeigename",
            OptionDataType.String, false, "", "Quartermaster",
            description: "Anzeigename der Anwendung, wird in Benachrichtigungstexten und Branding verwendet. Standard: Quartermaster.");

        AddDefinitionIfNotExists("messaging.telegram.bot_username",
            "Telegram: Bot-Benutzername",
            OptionDataType.String, false, "", "",
            description: "Telegram Bot-Benutzername ohne führendes @. Wird benötigt, um Deeplinks der Form https://t.me/<Benutzername>?start=<Token> aufzubauen, die Benutzer*innen zum Verknüpfen ihres Telegram-Accounts verwenden.");

        AddDefinitionIfNotExists("messaging.pdf.output_dir",
            "PDF-Ausdruck: Ausgabeverzeichnis",
            OptionDataType.String, false, "", "",
            description: "Absoluter Pfad zum Ordner, in dem generierte PDF-Briefe abgelegt werden. Leer ⇒ Standard 'data/printouts' relativ zum Anwendungsverzeichnis.");

        AddDefinitionIfNotExists("auth.token.lifetime_days",
            "Login-Token: Gültigkeitsdauer (Tage)",
            OptionDataType.Number, false, "", "7",
            description: "Lebensdauer eines Login-Tokens in Tagen. Bei jeder erfolgreichen Verwendung wird die Gültigkeit um diesen Zeitraum verlängert (Sliding-Window). Standard: 7.");

        AddDefinitionIfNotExists("auth.lockout.max_attempts",
            "Login-Sperre: Max. Fehlversuche",
            OptionDataType.Number, false, "", "5",
            description: "Anzahl fehlgeschlagener Login-Versuche (pro IP+Benutzer) innerhalb der Sperrdauer, bevor eine Sperre greift. Standard: 5.");

        AddDefinitionIfNotExists("auth.lockout.duration_minutes",
            "Login-Sperre: Sperrdauer (Minuten)",
            OptionDataType.Number, false, "", "15",
            description: "Zeitfenster und Sperrdauer in Minuten: Fehlversuche werden in diesem Fenster gezählt, und die Sperre gilt so lange nach dem letzten Fehlversuch. Standard: 15.");

        AddDefinitionIfNotExists("auth.ratelimit.anonymous_create_permits",
            "Rate-Limit: Anonyme Anfragen pro Fenster",
            OptionDataType.Number, false, "", "5",
            description: "Maximale Anzahl anonymer Erstell-Anfragen (Anträge, Mitgliedsanträge, Beitragseinstufungen, PDFs der Auslagen- und Reisekostenerstattung) pro IP innerhalb des Zeitfensters. Standard: 5. Greift bei neuen IPs sofort, bei aktiven IPs nach Ablauf ihres aktuellen Fensters.");

        AddDefinitionIfNotExists("auth.ratelimit.anonymous_create_window_minutes",
            "Rate-Limit: Zeitfenster (Minuten)",
            OptionDataType.Number, false, "", "10",
            description: "Länge des Zeitfensters für das Rate-Limit anonymer Anfragen, in Minuten. Standard: 10.");

        AddDefinitionIfNotExists("meetings.protocol.archive_dir",
            "Sitzungen: Protokoll-Archivpfad",
            OptionDataType.String, false, "", "",
            description: "Absoluter Pfad, unter dem archivierte Sitzungsprotokolle als PDF gespeichert werden. Standard: ./data/protocols");

        AddDefinitionIfNotExists("meetings.motion_notes_template",
            "Sitzungen: Antragsnotiz-Vorlage",
            OptionDataType.String, true,
            "MotionDTO",
            "**Antrag:** {{ motion.Title }}\n\n**Antragsteller:** {{ motion.AuthorName }}\n\n{{ motion.Text }}\n\n---\n\n**Diskussion:**\n",
            description: "Fluid-Template für vorausgefüllte Notizen bei Antrag-Tagesordnungspunkten. Verfügbare Variablen: motion.Title, motion.AuthorName, motion.AuthorEmail, motion.Text");

        AddDefinitionIfNotExists("meetings.collab.save_interval_seconds",
            "Sitzungen: Kollaborative Notizen – Speicherintervall (Sekunden)",
            OptionDataType.Number, false, "", "10",
            description: "Intervall in Sekunden, in dem der Server einen Snapshot der kollaborativ bearbeiteten Notizen persistiert. Niedrigere Werte reduzieren den Datenverlust bei Verbindungsabbrüchen, erhöhen aber die DB-Last. Standard: 10.");

        AddDefinitionIfNotExists("newsposts.enabled",
            "Social Media: Aktiviert",
            OptionDataType.String, false, "", "false",
            description: "Schaltet das Abrufen von Nachrichten und das Erzeugen von Vorschlägen ein (true/false).");

        AddDefinitionIfNotExists("newsposts.poll_interval_minutes",
            "Social Media: Abrufintervall",
            OptionDataType.Number, false, "", "15",
            description: "Intervall in Minuten, in dem die Nachrichtenquellen abgerufen und verarbeitet werden.");

        AddDefinitionIfNotExists("newsposts.max_item_age_hours",
            "Social Media: Maximales Meldungsalter",
            OptionDataType.Number, false, "", "48",
            description: "Ältere Meldungen werden beim Abruf ignoriert (Stunden).");

        AddDefinitionIfNotExists("newsposts.retention_days",
            "Social Media: Aufbewahrung",
            OptionDataType.Number, false, "", "30",
            description: "Meldungen ohne Veröffentlichung werden nach so vielen Tagen ohne Änderung gelöscht.");

        AddDefinitionIfNotExists("newsposts.mistral.api_key",
            "Social Media: Mistral API-Schlüssel",
            OptionDataType.String, false, "", "",
            isSecret: true);

        AddDefinitionIfNotExists("newsposts.mistral.base_url",
            "Social Media: Mistral API-URL",
            OptionDataType.String, false, "", "https://api.mistral.ai");

        AddDefinitionIfNotExists("newsposts.mistral.triage_model",
            "Social Media: Modell für Vorauswahl",
            OptionDataType.String, false, "", "mistral-small-latest");

        AddDefinitionIfNotExists("newsposts.mistral.generation_model",
            "Social Media: Modell für Vorschläge",
            OptionDataType.String, false, "", "mistral-medium-latest");

        AddDefinitionIfNotExists("newsposts.mistral.embedding_model",
            "Social Media: Embedding-Modell",
            OptionDataType.String, false, "", "mistral-embed");

        AddDefinitionIfNotExists("newsposts.triage.min_similarity",
            "Social Media: Mindestähnlichkeit",
            OptionDataType.String, false, "", "0.70",
            description: "Unterhalb dieser Kosinus-Ähnlichkeit zum Programm gilt eine Meldung ohne KI-Aufruf als nicht relevant (0 bis 1).");

        AddDefinitionIfNotExists("newsposts.generation.candidate_count",
            "Social Media: Anzahl Vorschläge",
            OptionDataType.Number, false, "", "5");

        AddDefinitionIfNotExists("newsposts.generation.prompt",
            "Social Media: Stilregeln",
            OptionDataType.String, false, "", NewsPostsDefaultPrompt,
            description: "Ton- und Stilregeln für die erzeugten Beiträge. Format- und Faktenregeln ergänzt die Anwendung selbst.");

        AddDefinitionIfNotExists("newsposts.postiz.base_url",
            "Social Media: Postiz API-URL",
            OptionDataType.String, false, "", "",
            description: "Basis-URL der Postiz-Instanz inklusive /api, z.B. https://postiz.example.org/api");

        AddDefinitionIfNotExists("newsposts.postiz.api_key",
            "Social Media: Postiz API-Schlüssel",
            OptionDataType.String, false, "", "",
            isSecret: true);

        AddDefinitionIfNotExists("newsposts.postiz.x_reply_setting",
            "Social Media: Antworten auf X",
            OptionDataType.String, false, "", "everyone",
            description: "Wer auf X antworten darf: everyone, following, mentionedUsers, subscribers oder verified.");

        AddDefinitionIfNotExists("keycloak.base_url",
            "Keycloak: Basis-URL",
            OptionDataType.String, false, "", "",
            description: "Basis-URL des Keycloak-Servers, z.B. https://sso.piratenpartei.de (für die Akkreditierung).");

        AddDefinitionIfNotExists("keycloak.auth_realm",
            "Keycloak: Realm des Admin-Clients",
            OptionDataType.String, false, "", "master");

        AddDefinitionIfNotExists("keycloak.realm",
            "Keycloak: Mitglieder-Realm",
            OptionDataType.String, false, "", "",
            description: "Realm, in dem die Mitgliederkonten liegen.");

        AddDefinitionIfNotExists("keycloak.client_id",
            "Keycloak: Client-ID",
            OptionDataType.String, false, "", "",
            description: "Client mit Client-Credentials und der Rolle manage-users im Mitglieder-Realm (derselbe Client wie beim Mitgliederdatenimport).");

        AddDefinitionIfNotExists("keycloak.client_secret",
            "Keycloak: Client-Secret",
            OptionDataType.String, false, "", "",
            isSecret: true);

        AddDefinitionIfNotExists("keycloak.username_separator",
            "Keycloak: Trennzeichen im Benutzernamen",
            OptionDataType.String, false, "", ".",
            description: "Trennzeichen zwischen Vor- und Nachname, wie beim Mitgliederdatenimport.");

        AddDefinitionIfNotExists(ReceiptOptions.MaxPerUser,
            "Belege: Höchstzahl je Person",
            OptionDataType.Number, false, "", "100",
            description: "So viele Belege und Streckenbilder kann eine Person gleichzeitig gespeichert haben. Personen mit der Berechtigung \"Belege: unbegrenzt hochladen\" sind ausgenommen.");

        AddDefinitionIfNotExists(ReceiptOptions.MaxFileMegabytes,
            "Belege: Höchstgröße je Datei (MB)",
            OptionDataType.Number, false, "", "10",
            description: "Größte erlaubte Datei für Belege (JPEG, PNG oder PDF) in Megabyte.");

        AddDefinitionIfNotExists("wiki.api_url",
            "Wiki: API-URL",
            OptionDataType.String, false, "", "https://wiki.piratenpartei.de/api.php",
            description: "URL der MediaWiki-API des Piratenwikis, über die Antragsseiten gespiegelt werden.");

        AddDefinitionIfNotExists("wiki.bot_username",
            "Wiki: Bot-Benutzername",
            OptionDataType.String, false, "", "",
            description: "Benutzername des Bot-Kontos für die Wiki-Synchronisation.");

        AddDefinitionIfNotExists("wiki.bot_password",
            "Wiki: Bot-Passwort",
            OptionDataType.String, false, "", "",
            description: "Passwort des Bot-Kontos für die Wiki-Synchronisation.",
            isSecret: true);

        AddDefinitionIfNotExists("wiki.protection_level",
            "Wiki: Schutzstufe für Antragsseiten",
            OptionDataType.String, false, "", "Antrag",
            description: "Schutzstufe, mit der Antragsseiten im Wiki vor Bearbeitung geschützt werden.");

        AddDefinitionIfNotExists("discourse.base_url",
            "Forum: Basis-URL",
            OptionDataType.String, false, "", "",
            description: "Basis-URL des Discourse-Forums, in das eingereichte Anträge gespiegelt werden, zum Beispiel https://forum.piratenpartei.de.");

        AddDefinitionIfNotExists("discourse.api_username",
            "Forum: Bot-Benutzername",
            OptionDataType.String, false, "", "",
            description: "Benutzername des Bot-Kontos im Forum. Das Konto muss Moderator sein, sonst begrenzt Discourse Themen, Bearbeitungen und Links.");

        AddDefinitionIfNotExists("discourse.api_key",
            "Forum: API-Schlüssel",
            OptionDataType.String, false, "", "",
            description: "API-Schlüssel, der an das Bot-Konto gebunden ist.",
            isSecret: true);

        AddDefinitionIfNotExists("discourse.max_post_length",
            "Forum: Maximale Beitragslänge",
            OptionDataType.Number, false, "", "32000",
            description: "Muss der Discourse-Einstellung max_post_length entsprechen. Passt der Antragstext nicht hinein, steht im Forum stattdessen ein Hinweis auf Antragsportal und Wiki.");

        AddDefinitionIfNotExists("openslides.url",
            "OpenSlides: Basis-URL",
            OptionDataType.String, false, "", "",
            description: "Basis-URL der OpenSlides-Instanz, an die Anträge übertragen werden.");

        AddDefinitionIfNotExists("openslides.username",
            "OpenSlides: Benutzername",
            OptionDataType.String, false, "", "",
            description: "Benutzername für die OpenSlides-API.");

        AddDefinitionIfNotExists("openslides.password",
            "OpenSlides: Passwort",
            OptionDataType.String, false, "", "",
            description: "Passwort für die OpenSlides-API.",
            isSecret: true);

        AddDefinitionIfNotExists("mumble.host",
            "Mumble: Server",
            OptionDataType.String, false, "", "mumble.piratenpartei.de",
            description: "Hostname des Mumble-Servers, mit dem sich der Versammlungsassistent verbindet.");

        AddDefinitionIfNotExists("mumble.port",
            "Mumble: Port",
            OptionDataType.Number, false, "", "64738",
            description: "TCP-Port des Mumble-Servers.");

        AddDefinitionIfNotExists("mumble.username",
            "Mumble: Name des Bots",
            OptionDataType.String, false, "", "Versammlungsassistent",
            description: "Name, unter dem der Versammlungsassistent im Mumble erscheint.");

        AddDefinitionIfNotExists("mumble.certificate",
            "Mumble: Client-Zertifikat",
            OptionDataType.String, false, "", "",
            description: "PKCS#12-Zertifikat des Bots als Base64; wird auf der Seite Versammlungsassistent erzeugt.",
            isSecret: true);

        AddDefinitionIfNotExists("mumble.certificate_password",
            "Mumble: Zertifikatspasswort",
            OptionDataType.String, false, "", "",
            description: "Passwort des PKCS#12-Zertifikats des Bots.",
            isSecret: true);

        AddDefinitionIfNotExists("mumble.server_certificate_sha256",
            "Mumble: Fingerabdruck des Serverzertifikats",
            OptionDataType.String, false, "", "",
            description: "Leer = Systemvertrauen; gesetzt = nur ein Serverzertifikat mit diesem SHA-256-Fingerabdruck wird akzeptiert.");

        AddDefinitionIfNotExists("mumble.channels.chair",
            "Mumble: Kanal Versammlungsleitung",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Versammlungsleitung; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.speaker",
            "Mumble: Kanal Redner",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Redner; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.motion_author",
            "Mumble: Kanal Antragsteller",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Antragsteller; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.pro",
            "Mumble: Kanal Wortmeldung / Pro",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Wortmeldung / Pro; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.contra",
            "Mumble: Kanal Contra",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Contra; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.question",
            "Mumble: Kanal Frage",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Frage; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.point",
            "Mumble: Kanal GO-Antrag",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle GO-Antrag; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("mumble.channels.listener",
            "Mumble: Kanal Zuhörer",
            OptionDataType.String, false, "", "",
            description: "Kanal-ID für die Rolle Zuhörer; wird auf der Seite Versammlungsassistent gesetzt.");

        AddDefinitionIfNotExists("assembly.times.presentation",
            "Versammlung: Redezeit Antragsvorstellung",
            OptionDataType.Number, false, "", "300",
            description: "Standardredezeit in Sekunden für Antragsvorstellung.");

        AddDefinitionIfNotExists("assembly.times.speech",
            "Versammlung: Redezeit Redebeitrag",
            OptionDataType.Number, false, "", "180",
            description: "Standardredezeit in Sekunden für Redebeitrag.");

        AddDefinitionIfNotExists("assembly.times.question",
            "Versammlung: Redezeit Frage",
            OptionDataType.Number, false, "", "30",
            description: "Standardredezeit in Sekunden für Frage.");

        AddDefinitionIfNotExists("assembly.times.statement",
            "Versammlung: Redezeit Stellungnahme",
            OptionDataType.Number, false, "", "60",
            description: "Standardredezeit in Sekunden für Stellungnahme.");

        AddDefinitionIfNotExists("assembly.times.closing_word",
            "Versammlung: Redezeit Schlusswort",
            OptionDataType.Number, false, "", "180",
            description: "Standardredezeit in Sekunden für Schlusswort.");

        AddDefinitionIfNotExists("assembly.times.candidate_presentation",
            "Versammlung: Redezeit Kandidatenvorstellung",
            OptionDataType.Number, false, "", "300",
            description: "Standardredezeit in Sekunden für Kandidatenvorstellung.");

        AddDefinitionIfNotExists("assembly.times.candidate_presentation_repeat",
            "Versammlung: Redezeit erneute Kandidatenvorstellung",
            OptionDataType.Number, false, "", "180",
            description: "Standardredezeit in Sekunden für erneute Kandidatenvorstellung.");

        AddDefinitionIfNotExists("assembly.times.candidate_question",
            "Versammlung: Redezeit Frage an Kandidierende",
            OptionDataType.Number, false, "", "30",
            description: "Standardredezeit in Sekunden für Frage an Kandidierende.");

        AddDefinitionIfNotExists("assembly.times.candidate_answer",
            "Versammlung: Redezeit Antwort von Kandidierenden",
            OptionDataType.Number, false, "", "120",
            description: "Standardredezeit in Sekunden für Antwort von Kandidierenden.");

        AddDefinitionIfNotExists("assembly.grace_seconds",
            "Versammlung: Karenzzeit Wortmeldung",
            OptionDataType.Number, false, "", "30",
            description: "Sekunden, in denen eine Wortmeldung nach Verlassen des Kanals oder Verbindungsabbruch ihren Platz behält.");

        AddDefinitionIfNotExists("assembly.voice_hangover_ms",
            "Versammlung: Sprecherkennung Nachlauf",
            OptionDataType.Number, false, "", "500",
            description: "Millisekunden ohne Sprachpaket, nach denen eine Person nicht mehr als sprechend gilt.");

        AddDefinitionIfNotExists("assembly.start_now_after_seconds",
            "Versammlung: Timer-Start anbieten nach",
            OptionDataType.Number, false, "", "20",
            description: "Sekunden ohne Sprachpaket nach dem Rederecht, nach denen die Konsole Jetzt starten anbietet.");

        AddDefinitionIfNotExists("assembly.messages.warning",
            "Versammlung: Text Redezeitwarnung",
            OptionDataType.String, false, "", "Noch 30 Sekunden",
            description: "Mumble-Nachricht an die redende Person 30 Sekunden vor Ende der Redezeit.");

        AddDefinitionIfNotExists("assembly.messages.expired",
            "Versammlung: Text Redezeit abgelaufen",
            OptionDataType.String, false, "", "Die Redezeit ist abgelaufen",
            description: "Mumble-Nachricht an die redende Person bei Ablauf der Redezeit.");

        AddDefinitionIfNotExists("assembly.openslides.delegates_group_id",
            "Versammlung: OpenSlides-Gruppe Stimmberechtigte",
            OptionDataType.Number, false, "", "",
            description: "ID der OpenSlides-Gruppe (delegates), die bei jeder Abstimmung stimmberechtigt ist.");

        AddDefinitionIfNotExists("assembly.poll.duration_seconds",
            "Versammlung: Abstimmungsdauer",
            OptionDataType.Number, false, "", "70",
            description: "Standarddauer einer Abstimmung in Sekunden (60 s plus 10 s Streamverzögerung).");

        AddDefinitionIfNotExists("assembly.poll.auto_start",
            "Versammlung: Abstimmung automatisch starten",
            OptionDataType.String, false, "", "true",
            description: "true = vom Assistenten angelegte Abstimmungen starten sofort.");

        AddDefinitionIfNotExists("assembly.poll.auto_stop",
            "Versammlung: Abstimmung automatisch beenden",
            OptionDataType.String, false, "", "true",
            description: "true = Abstimmung wird bei Ablauf des Countdowns beendet.");

        AddDefinitionIfNotExists("assembly.poll.project",
            "Versammlung: Abstimmung auf dem Beamer zeigen",
            OptionDataType.String, false, "", "false",
            description: "true = eine gestartete Abstimmung wird auf den OpenSlides-Projektoren aus assembly.openslides.projector_ids gezeigt.");

        AddDefinitionIfNotExists("assembly.openslides.projector_ids",
            "Versammlung: OpenSlides-Projektoren",
            OptionDataType.String, false, "", "",
            description: "Kommagetrennte IDs der OpenSlides-Projektoren, auf denen laufende Abstimmungen gezeigt werden.");

        AddDefinitionIfNotExists("assembly.poll.auto_publish",
            "Versammlung: Ergebnis automatisch veröffentlichen",
            OptionDataType.String, false, "", "true",
            description: "true = Ergebnis wird nach dem Beenden sofort veröffentlicht.");
    }

    private const string NewsPostsDefaultPrompt =
        "Du schreibst Beiträge für die Social-Media-Kanäle der Piratenpartei Deutschland. " +
        "Klar, pointiert, sachlich im Kern, gern mit einer zugespitzten Pointe. " +
        "Sprich als \"wir PIRATEN\", nicht als Einzelperson. Keine Beleidigungen, keine Unterstellungen. " +
        "Höchstens zwei Hashtags, bevorzugt #Piraten plus ein Themen-Hashtag.";

    private void AddDefinitionIfNotExists(string identifier, string friendlyName,
        OptionDataType dataType, bool isOverridable, string templateModels, string defaultValue,
        string description = "", bool isSecret = false) {

        var existing = GetDefinition(identifier);
        if (existing != null) {
            var nameChanged = existing.FriendlyName != friendlyName;
            var descChanged = !string.IsNullOrEmpty(description) && existing.Description != description;
            var secretChanged = existing.IsSecret != isSecret;
            if (nameChanged || descChanged || secretChanged) {
                _context.OptionDefinitions
                    .Where(d => d.Id == existing.Id)
                    .Set(d => d.FriendlyName, friendlyName)
                    .Set(d => d.Description, description)
                    .Set(d => d.IsSecret, isSecret)
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
            TemplateModels = templateModels,
            IsSecret = isSecret
        });

        if (!string.IsNullOrEmpty(defaultValue))
            SetValue(identifier, null, defaultValue);
    }
}
