using System.Collections.Generic;

namespace Quartermaster.Data.Templates;

internal record SystemTemplateSeed(
    string Identifier,
    string DisplayName,
    string? Subject,
    string Body,
    bool AllowsChapterFields = true,
    bool AllowsMemberFields = false,
    bool AllowsEventFields = false);

internal static class SystemTemplateSeeds {
    public static readonly IReadOnlyList<SystemTemplateSeed> All = new[] {
        new SystemTemplateSeed(
            "forwarding.redmine.application",
            "Redmine-Ticket: Mitgliedsantrag",
            "Mitgliedsantrag: {{ application.FirstName }} {{ application.LastName }} ({{ application.ChapterName }})",
            "**Mitgliedsantrag** von {{ application.FirstName }} {{ application.LastName }}\n\n- Geburtsdatum: {{ application.DateOfBirth }}\n- Staatsangehörigkeit: {{ application.Citizenship }}\n- E-Mail: {{ application.Email }}\n- Telefon: {{ application.PhoneNumber }}\n- Anschrift: {{ application.Street }} {{ application.HouseNumber }}, {{ application.PostCode }} {{ application.City }}\n- Gliederung: {{ application.ChapterName }}\n- Gewünschtes Eintrittsdatum: {{ application.EntryDate }}\n- Mitglied einer anderen Partei: {% if application.IsMemberOfAnotherParty %}ja{% else %}nein{% endif %}\n- Früher abgelehnter Mitgliedsantrag: {% if application.HasPriorDeclinedApplication %}ja{% else %}nein{% endif %}\n{% if application.ApplicationText != \"\" %}\n**Anmerkungen:** {{ application.ApplicationText }}\n{% endif %}\n**Beitrag**\n\n- Einstufung: {{ dues.Valuation }}\n- Jahresbeitrag: {{ dues.YearlyAmount }} €\n{% if dues.Reduced %}- Beantragte Minderung auf {{ dues.ReducedAmount }} € ({{ dues.ReducedTimeSpan }}), Begründung: {{ dues.Justification }}{% if case.RelatedReference != \"\" %} (eigener Vorgang {{ case.RelatedReference }}){% endif %}\n{% endif %}- Zahlweise: {{ dues.PaymentSchedule }}\n{% if dues.IsDirectDeposit %}- Lastschrift: {{ dues.AccountHolder }}, IBAN {{ dues.Iban }}\n{% else %}- Zahlung per Überweisung\n{% endif %}{% if federalTicket.Url != \"\" %}\nTicket der Bundesebene: {{ federalTicket.Url }}\n{% endif %}\nEingegangen am {{ submission.SubmittedAt }}\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "forwarding.redmine.due_selection",
            "Redmine-Ticket: Beitragseinstufung",
            "Beitragseinstufung: {{ member.FirstName }} {{ member.LastName }} ({{ member.Number }})",
            "**Beitragseinstufung** von {{ member.FirstName }} {{ member.LastName }}\n\n- Mitgliedsnummer: {{ member.Number }}\n- E-Mail: {{ member.Email }}\n- Einstufung: {{ dues.Valuation }}\n- Jahresbeitrag: {{ dues.YearlyAmount }} €\n- Zahlweise: {{ dues.PaymentSchedule }}\n{% if dues.IsDirectDeposit %}- Lastschrift: {{ dues.AccountHolder }}, IBAN {{ dues.Iban }}\n{% else %}- Zahlung per Überweisung\n{% endif %}\nEingereicht am {{ submission.SubmittedAt }}{% if submission.SubmittedBy != \"\" %} über das Konto {{ submission.SubmittedBy }}{% endif %}\n{% if submission.EnteredByHand %}\nDie Personendaten wurden von Hand eingegeben, das Konto ist mit keinem Mitglied verknüpft.\n{% endif %}",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "forwarding.redmine.reduction",
            "Redmine-Ticket: Beitragsminderung",
            "Beitragsminderung: {{ member.FirstName }} {{ member.LastName }} auf {{ dues.ReducedAmount }} €",
            "**Antrag auf Beitragsminderung** von {{ member.FirstName }} {{ member.LastName }}\n\n- Mitgliedsnummer: {{ member.Number }}\n- E-Mail: {{ member.Email }}\n- Gewünschter Jahresbeitrag: {{ dues.ReducedAmount }} € ({{ dues.ReducedTimeSpan }})\n- Begründung: {{ dues.Justification }}\n- Zahlweise: {{ dues.PaymentSchedule }}\n{% if dues.IsDirectDeposit %}- Lastschrift: {{ dues.AccountHolder }}, IBAN {{ dues.Iban }}\n{% else %}- Zahlung per Überweisung\n{% endif %}- Entscheidet: {{ chapter.DecidingName }}\n{% if case.RelatedReference != \"\" %}- Gehört zum Mitgliedsantrag {{ case.RelatedReference }}\n{% endif %}{% if federalTicket.Url != \"\" %}\nTicket der Bundesebene: {{ federalTicket.Url }}\n{% endif %}\nEingereicht am {{ submission.SubmittedAt }}{% if submission.SubmittedBy != \"\" %} über das Konto {{ submission.SubmittedBy }}{% endif %}\n{% if submission.EnteredByHand %}\nDie Personendaten wurden von Hand eingegeben, das Konto ist mit keinem Mitglied verknüpft.\n{% endif %}",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "forwarding.redmine.outcome_note",
            "Redmine-Notiz: Entscheidung in Quartermaster",
            null,
            "Entscheidung in Quartermaster: **{{ outcome.Result }}** durch {{ outcome.DecidedBy }} am {{ outcome.Date }}.\n\nBeschluss: {{ outcome.MotionUrl }}\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "forwarding.failure.email",
            "E-Mail: Weiterleitung fehlgeschlagen",
            "Weiterleitung fehlgeschlagen: {{ case.Reference }}",
            "Für den Vorgang **{{ case.Reference }}** ({{ case.Kind }}) konnte das Ticket \"{{ failure.Purpose }}\" bei **{{ failure.TargetChapter }}** nicht angelegt werden.\n\n**Fehler:** {{ failure.Error }}\n\nBitte das Ticket von Hand anlegen und den Vorgang danach unter {{ failure.CasesUrl }} als erledigt markieren, oder die Ursache beheben und dort erneut versuchen.\n\n---\n\n**Betreff:** {{ failure.TicketSubject }}\n\n{{ failure.TicketDescription }}\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "templates.membershipapplication.approved.email",
            "E-Mail: Mitgliedsantrag genehmigt",
            "Dein Mitgliedsantrag wurde genehmigt",
            "Hallo **{{ application.FirstName }}**,\n\ndein Mitgliedsantrag bei der **{{ chapter.Name }}** wurde genehmigt.\n\nWillkommen an Bord!\n"),
        new SystemTemplateSeed(
            "templates.membershipapplication.rejected.email",
            "E-Mail: Mitgliedsantrag abgelehnt",
            "Dein Mitgliedsantrag wurde abgelehnt",
            "Hallo **{{ application.FirstName }}**,\n\nleider wurde dein Mitgliedsantrag bei der **{{ chapter.Name }}** abgelehnt.\n"),
        new SystemTemplateSeed(
            "templates.dueselection.approved.email",
            "E-Mail: Beitragsminderung genehmigt",
            "Deine Beitragsminderung wurde genehmigt",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde genehmigt.\n"),
        new SystemTemplateSeed(
            "templates.dueselection.rejected.email",
            "E-Mail: Beitragsminderung abgelehnt",
            "Deine Beitragsminderung wurde abgelehnt",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde leider abgelehnt.\n"),
        new SystemTemplateSeed(
            "templates.submission.motion.confirmation.email",
            "E-Mail: Antrag bestätigen",
            "Bitte bestätige deinen Antrag",
            "Hallo {{ motion.AuthorName }},\n\nbitte bestätige deine E-Mail-Adresse, damit dein Antrag bearbeitet wird:\n\n**[Antrag jetzt bestätigen]({{ confirm.Url }})**\n\n---\n\n**Zusammenfassung**\n\n- **Gliederung:** {{ chapter.Name }}\n- **Titel:** {{ motion.Title }}\n\nWenn du diesen Antrag nicht eingereicht hast, ignoriere diese E-Mail – ohne Bestätigung wird nichts gespeichert.\n"),
        new SystemTemplateSeed(
            "templates.submission.dueselection.confirmation.email",
            "E-Mail: Beitragseinstufung bestätigen",
            "Bitte bestätige deine Beitragseinstufung",
            "Hallo {{ selection.FirstName }},\n\nbitte bestätige deine E-Mail-Adresse, damit deine Beitragseinstufung bearbeitet wird:\n\n**[Einstufung jetzt bestätigen]({{ confirm.Url }})**\n\n---\n\n**Zusammenfassung**\n\n- **Gewählter Beitrag:** {{ selection.SelectedDue }}€\n\nWenn du das nicht eingereicht hast, ignoriere diese E-Mail – ohne Bestätigung wird nichts gespeichert.\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "templates.submission.membershipapplication.confirmation.email",
            "E-Mail: Mitgliedsantrag bestätigen",
            "Bitte bestätige deinen Mitgliedsantrag",
            "Hallo {{ application.FirstName }},\n\nbitte bestätige deine E-Mail-Adresse, damit dein Mitgliedsantrag bearbeitet wird:\n\n**[Mitgliedsantrag jetzt bestätigen]({{ confirm.Url }})**\n\n---\n\n**Zusammenfassung**\n\n- **Name:** {{ application.FirstName }} {{ application.LastName }}\n- **Gliederung:** {{ chapter.Name }}\n\nWenn du diesen Antrag nicht eingereicht hast, ignoriere diese E-Mail – ohne Bestätigung wird nichts gespeichert.\n"),
        new SystemTemplateSeed(
            "templates.membershipapplication.received.email",
            "E-Mail: Mitgliedsantrag eingegangen",
            "Dein Mitgliedsantrag ist eingegangen",
            "Hallo {{ application.FirstName }},\n\nvielen Dank für deinen Mitgliedsantrag bei der **{{ chapter.Name }}**. Dein Antrag ist bei uns eingegangen und wird vom Vorstand geprüft.\n\nWir melden uns, sobald über deinen Antrag entschieden wurde.\n\nViele Grüße\n{{ globals.AppName }}\n"),
        new SystemTemplateSeed(
            "templates.member.welcome.email",
            "E-Mail: Willkommen als Mitglied",
            "Willkommen als Mitglied",
            "Hallo {{ member.FirstName }},\n\nherzlich willkommen! Dein Mitgliedsantrag wurde angenommen und du bist nun Mitglied bei der **{{ chapter.Name }}**.\n\nDeine Mitgliedsnummer lautet: **{{ member.MemberNumber }}**\n\nViele Grüße\n{{ globals.AppName }}\n",
            AllowsMemberFields: true),
        new SystemTemplateSeed(
            "notifications.motion_submitted.email",
            "Benachrichtigung: Neuer Antrag (E-Mail)",
            "Neuer Antrag: {{ motion.Title }}",
            "Hallo,\n\nfür die Gliederung **{{ chapter.Name }}** wurde ein neuer Antrag eingereicht:\n\n*{{ motion.Title }}*\n\nEingereicht von: {{ motion.AuthorName }}\n\n[Antrag öffnen]({{ globals.BaseUrl }}/Administration/Motions/{{ motion.Id }})\n"),
        new SystemTemplateSeed(
            "notifications.application_submitted.email",
            "Benachrichtigung: Neuer Mitgliedsantrag (E-Mail)",
            "Neuer Mitgliedsantrag: {{ application.FirstName }} {{ application.LastName }}",
            "Hallo,\n\nfür die Gliederung **{{ chapter.Name }}** ist ein neuer Mitgliedsantrag eingegangen:\n\n*{{ application.FirstName }} {{ application.LastName }}* ({{ application.Email }})\n{% if application.HasReducedDueSelection %}\nDer Antrag enthält einen Antrag auf Beitragsminderung.\n{% endif %}\n[Mitgliedsantrag öffnen]({{ globals.BaseUrl }}/Administration/MembershipApplications/{{ application.Id }})\n"),
        new SystemTemplateSeed(
            "notifications.due_selection_submitted.email",
            "Benachrichtigung: Neue Beitragseinstufung (E-Mail)",
            "Neue Beitragsminderung: {{ selection.FirstName }} {{ selection.LastName }}",
            "Hallo,\n\nfür die Gliederung **{{ chapter.Name }}** ist eine neue Beitragsminderung eingegangen:\n\n*{{ selection.FirstName }} {{ selection.LastName }}*\n\nGewünschter Betrag: {{ selection.ReducedAmount }}€\nBegründung: {{ selection.ReducedJustification }}\n\n[Beitragseinstufung öffnen]({{ globals.BaseUrl }}/Administration/DueSelections/{{ selection.Id }})\n"),
        new SystemTemplateSeed(
            "notifications.motion_submitted.telegram",
            "Benachrichtigung: Neuer Antrag (Telegram)",
            null,
            "Neuer Antrag in *{{ chapter.Name }}*\n\n*{{ motion.Title }}*\nEingereicht von {{ motion.AuthorName }}\n\n[Antrag öffnen]({{ globals.BaseUrl }}/Administration/Motions/{{ motion.Id }})"),
        new SystemTemplateSeed(
            "notifications.application_submitted.telegram",
            "Benachrichtigung: Neuer Mitgliedsantrag (Telegram)",
            null,
            "Neuer Mitgliedsantrag in *{{ chapter.Name }}*\n\n*{{ application.FirstName }} {{ application.LastName }}*\n\n[Mitgliedsantrag öffnen]({{ globals.BaseUrl }}/Administration/MembershipApplications/{{ application.Id }})"),
        new SystemTemplateSeed(
            "notifications.due_selection_submitted.telegram",
            "Benachrichtigung: Neue Beitragseinstufung (Telegram)",
            null,
            "Neue Beitragsminderung in *{{ chapter.Name }}*\n\n*{{ selection.FirstName }} {{ selection.LastName }}* — {{ selection.ReducedAmount }}€\n\n[Beitragseinstufung öffnen]({{ globals.BaseUrl }}/Administration/DueSelections/{{ selection.Id }})"),
        new SystemTemplateSeed(
            "notifications.assembly_motion_cosubmitter_reset.email",
            "Benachrichtigung: Antrag geändert, Mitantragstellung zurückgesetzt (E-Mail)",
            "Antrag geändert: {{ motion.Title }}",
            "Hallo,\n\nder Antrag *{{ motion.TitleEscaped }}* wurde von der antragstellenden Person geändert. Deine Mitantragstellung wurde deshalb zurückgesetzt.\n\nWenn du den geänderten Antrag weiter unterstützen möchtest, tritt ihm bitte erneut bei:\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_collecting_reminder.email",
            "Benachrichtigung: Erinnerung: Mitantragstellende gesucht (E-Mail)",
            "Erinnerung: Mitantragstellende für {{ motion.Title }} gesucht",
            "Hallo,\n\nfür deinen Antrag *{{ motion.TitleEscaped }}* ({{ motion.TypeName }}, {{ motion.EventName }}) fehlen noch Mitantragstellende. Die Frist endet am {{ motion.Deadline }}.\n\nWerden bis dahin nicht genug Mitantragstellende gefunden, verfällt der Antrag.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_submitted.email",
            "Benachrichtigung: Antrag eingereicht (Antragskommission) (E-Mail)",
            "Antrag eingereicht: {{ motion.DisplayNumber }} {{ motion.Title }}",
            "Hallo,\n\nzur Versammlung {{ motion.EventName }} wurde ein Antrag eingereicht:\n\n*{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}* ({{ motion.TypeName }})\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_lapsed.email",
            "Benachrichtigung: Antrag verfallen (E-Mail)",
            "Antrag verfallen: {{ motion.Title }}",
            "Hallo,\n\nder Antrag *{{ motion.TitleEscaped }}* ({{ motion.TypeName }}, {{ motion.EventName }}) ist verfallen, weil die Frist am {{ motion.Deadline }} ohne genug Mitantragstellende abgelaufen ist.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_checked.email",
            "Benachrichtigung: Antrag geprüft (E-Mail)",
            "Antrag geprüft: {{ motion.DisplayNumber }} {{ motion.Title }}",
            "Hallo,\n\ndie Antragskommission hat den Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}* geprüft.\n\nErgebnis: **{{ motion.StatusLabel }}**\n{% if motion.CheckNote != \"\" %}\nHinweis der Antragskommission:\n\n{{ motion.CheckNote }}\n{% endif %}\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_inquiry_asked.email",
            "Benachrichtigung: Rückfrage zu deinem Antrag (E-Mail)",
            "Rückfrage zu deinem Antrag: {{ motion.Title }}",
            "Hallo,\n\ndie Antragskommission hat eine Rückfrage zum Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}*:\n\n{{ motion.InquiryQuestion }}\n\nBitte antworte bis {{ motion.InquiryDueAt }}:\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_inquiry_answered.email",
            "Benachrichtigung: Rückfrage beantwortet (Antragskommission) (E-Mail)",
            "Rückfrage beantwortet: {{ motion.DisplayNumber }} {{ motion.Title }}",
            "Hallo,\n\ndie Rückfrage zum Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}* wurde beantwortet.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_edited_by_commission.email",
            "Benachrichtigung: Antrag von der Antragskommission bearbeitet (E-Mail)",
            "Antrag bearbeitet: {{ motion.DisplayNumber }} {{ motion.Title }}",
            "Hallo,\n\ndie Antragskommission hat den Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}* bearbeitet. Deine Mitantragstellung bleibt bestehen.\n\nÄnderungshinweis:\n\n{{ motion.ChangeNote }}\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_competition_declared.email",
            "Benachrichtigung: Konkurrenzantrag erklärt (E-Mail)",
            "Konkurrenzantrag erklärt: {{ motion.Title }}",
            "Hallo,\n\nzu deinem Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}* wurde der Antrag *{{ motion.OtherDisplayNumber }} {{ motion.OtherTitleEscaped }}* als Konkurrenzantrag erklärt.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_withdrawn.email",
            "Benachrichtigung: Antrag zurückgezogen (E-Mail)",
            "Antrag zurückgezogen: {{ motion.Title }}",
            "Hallo,\n\nder Antrag *{{ motion.TitleEscaped }}* ({{ motion.TypeName }}, {{ motion.EventName }}) wurde zurückgezogen.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_cosubmitter_reset.telegram",
            "Benachrichtigung: Antrag geändert, Mitantragstellung zurückgesetzt (Telegram)",
            null,
            "Antrag geändert: *{{ motion.TitleEscaped }}*\n\nDeine Mitantragstellung wurde zurückgesetzt. Tritt dem Antrag erneut bei, wenn du ihn weiter unterstützen möchtest.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_collecting_reminder.telegram",
            "Benachrichtigung: Erinnerung: Mitantragstellende gesucht (Telegram)",
            null,
            "Erinnerung: Für deinen Antrag *{{ motion.TitleEscaped }}* fehlen noch Mitantragstellende. Die Frist endet am {{ motion.Deadline }}.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_submitted.telegram",
            "Benachrichtigung: Antrag eingereicht (Antragskommission) (Telegram)",
            null,
            "Antrag eingereicht ({{ motion.EventName }})\n\n*{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}*\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_lapsed.telegram",
            "Benachrichtigung: Antrag verfallen (Telegram)",
            null,
            "Antrag verfallen: *{{ motion.TitleEscaped }}*\n\nDie Frist ist ohne genug Mitantragstellende abgelaufen.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_checked.telegram",
            "Benachrichtigung: Antrag geprüft (Telegram)",
            null,
            "Antrag geprüft: *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}*\n\nErgebnis: {{ motion.StatusLabel }}\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_inquiry_asked.telegram",
            "Benachrichtigung: Rückfrage zu deinem Antrag (Telegram)",
            null,
            "Rückfrage zu deinem Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}*\n\nBitte antworte bis {{ motion.InquiryDueAt }}.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_inquiry_answered.telegram",
            "Benachrichtigung: Rückfrage beantwortet (Antragskommission) (Telegram)",
            null,
            "Rückfrage beantwortet: *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}*\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_edited_by_commission.telegram",
            "Benachrichtigung: Antrag von der Antragskommission bearbeitet (Telegram)",
            null,
            "Antrag bearbeitet: *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}*\n\nÄnderungshinweis: {{ motion.ChangeNote }}\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_competition_declared.telegram",
            "Benachrichtigung: Konkurrenzantrag erklärt (Telegram)",
            null,
            "Konkurrenzantrag erklärt\n\nZu deinem Antrag *{{ motion.DisplayNumber }} {{ motion.TitleEscaped }}* konkurriert jetzt *{{ motion.OtherDisplayNumber }} {{ motion.OtherTitleEscaped }}*.\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false),
        new SystemTemplateSeed(
            "notifications.assembly_motion_withdrawn.telegram",
            "Benachrichtigung: Antrag zurückgezogen (Telegram)",
            null,
            "Antrag zurückgezogen: *{{ motion.TitleEscaped }}*\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})",
            AllowsChapterFields: false)
    };
}
