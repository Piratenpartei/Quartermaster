using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public static class AssemblyMotionWikiTemplates {
    private const string ConstitutionalAmendment = """
        {% if motion.InRevision %}'''Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller. Aktueller Stand im Antragsportal: {{ motion.PortalUrl }}'''

        {% endif %}{{ "{{" }}Satzungsänderungsantrag BPT
        |bundesparteitag={{ assembly.Label }}
        |autor=Im Antragsportal einsehbar (Login erforderlich): {{ motion.PortalUrl }}
        |antragstyp={{ type.Name }}
        |antragsgruppe={{ motion.Section }}
        |titel={{ motion.Title }}
        |text={{ motion.Text }}
        |begruendung={{ motion.Reason }}
        |diskussionVorher={{ motion.PriorDiscussion }}
        |schlagworte={{ motion.Keywords }}
        |zusammenfassung={{ motion.Summary }}
        |konkurrenz={{ motion.CompetingNumbers }}
        |prüficon={{ motion.CheckIcon }}
        |abstimmung={{ motion.VoteResult }}
        |wikiBenutzer={{ bot.UserName }}
        {{ "}}" }}
        """;

    private const string ProgramMotion = """
        {% if motion.InRevision %}'''Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller. Aktueller Stand im Antragsportal: {{ motion.PortalUrl }}'''

        {% endif %}{{ "{{" }}Programmantrag BPT
        |bundesparteitag={{ assembly.Label }}
        |autor=Im Antragsportal einsehbar (Login erforderlich): {{ motion.PortalUrl }}
        |antragstyp={{ type.Name }}
        |antragsgruppe={{ motion.Section }}
        |titel={{ motion.Title }}
        |text={{ motion.Text }}
        |begruendung={{ motion.Reason }}
        |diskussionVorher={{ motion.PriorDiscussion }}
        |schlagworte={{ motion.Keywords }}
        |zusammenfassung={{ motion.Summary }}
        |konkurrenz={{ motion.CompetingNumbers }}
        |prüficon={{ motion.CheckIcon }}
        |abstimmung={{ motion.VoteResult }}
        |wikiBenutzer={{ bot.UserName }}
        {{ "}}" }}
        """;

    private const string OtherMotion = """
        {% if motion.InRevision %}'''Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller. Aktueller Stand im Antragsportal: {{ motion.PortalUrl }}'''

        {% endif %}{{ "{{" }}Sonstiger Antrag BPT
        |bundesparteitag={{ assembly.Label }}
        |autor=Im Antragsportal einsehbar (Login erforderlich): {{ motion.PortalUrl }}
        |antragstyp={{ type.Name }}
        |artantrag={{ type.Name }}
        |antragsgruppe={{ motion.Section }}
        |titel={{ motion.Title }}
        |text={{ motion.Text }}
        |begruendung={{ motion.Reason }}
        |diskussionVorher={{ motion.PriorDiscussion }}
        |schlagworte={{ motion.Keywords }}
        |zusammenfassung={{ motion.Summary }}
        |konkurrenz={{ motion.CompetingNumbers }}
        |prüficon={{ motion.CheckIcon }}
        |abstimmung={{ motion.VoteResult }}
        |wikiBenutzer={{ bot.UserName }}
        {{ "}}" }}
        """;

    public static string DefaultFor(string typeCode) => TextNormalization.ToNfc(typeCode) switch {
        "SÄA" => ConstitutionalAmendment,
        "GP" => ProgramMotion,
        "WP" => ProgramMotion,
        _ => OtherMotion
    };
}
