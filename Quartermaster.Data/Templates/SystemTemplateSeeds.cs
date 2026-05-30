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
            "Neue Beitragsminderung in *{{ chapter.Name }}*\n\n*{{ selection.FirstName }} {{ selection.LastName }}* — {{ selection.ReducedAmount }}€\n\n[Beitragseinstufung öffnen]({{ globals.BaseUrl }}/Administration/DueSelections/{{ selection.Id }})")
    };
}
