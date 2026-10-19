using System;
using System.Collections.Generic;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>
/// The travel expense rules published with the official forms in the Piratenwiki. The federal rules are seeded as the
/// global default; the state rules are offered when a chapter gets rules of its own.
/// </summary>
public static class TravelExpenseRulePresets {
    public const string DefaultTemplateKey = "travel-expense";

    public static readonly IReadOnlyList<TravelExpenseRulePreset> All = [
        new("federal", "Piratenpartei Deutschland (J2.0)", Federal),
        new("bavaria", "Landesverband Bayern (2.0-LV-BY)", Bavaria),
        new("baden-wuerttemberg", "Landesverband Baden-Württemberg (BW1.0)", BadenWuerttemberg),
        new("lower-saxony", "Landesverband Niedersachsen (J1.1)", LowerSaxony)
    ];

    /// <summary>Reisekostenordnung of 15.02.2024 with form J2.0, valid from 01.01.2024.</summary>
    public static TravelExpenseRulesDTO Federal() {
        var rules = EStGBase();
        rules.ValidFrom = new DateOnly(2024, 1, 1);
        rules.VersionLabel = "J2.0";
        rules.CarRates = [new RateStepDTO { RatePerKm = 0.30m }];
        rules.MotorcycleRates = [new RateStepDTO { RatePerKm = 0.20m }];
        rules.SubmissionDeadlineDays = 56;
        rules.CutoffDay = 28;
        rules.CutoffMonth = 2;
        rules.RulesText = FederalRulesText;
        return rules;
    }

    /// <summary>Reisekostenordnung of the LaVo Bayern of 01.04.2022 with form 2.0-LV-BY.</summary>
    public static TravelExpenseRulesDTO Bavaria() {
        var rules = EStGBase();
        rules.ValidFrom = new DateOnly(2022, 4, 1);
        rules.VersionLabel = "2.0-LV-BY";
        rules.HeaderLines = "Piratenpartei Deutschland LV Bayern - Geschäftsstelle - Schopenhauerstr. 71 - 80807 München\n"
            + "An den Schatzmeister im Landesvorstand (schatzmeister@piratenpartei-bayern.de)";
        rules.CarRates = [new RateStepDTO { RatePerKm = 0.30m }];
        rules.MotorcycleRates = [new RateStepDTO { RatePerKm = 0.13m }];
        rules.SubmissionDeadlineDays = 30;
        rules.CutoffDay = 15;
        rules.CutoffMonth = 2;
        rules.RulesText = BavariaRulesText;
        return rules;
    }

    /// <summary>Form BW1.0 for trips from 09.06.2021; the form names no deadlines, so the federal ones apply.</summary>
    public static TravelExpenseRulesDTO BadenWuerttemberg() {
        var rules = EStGBase();
        rules.ValidFrom = new DateOnly(2021, 6, 9);
        rules.VersionLabel = "BW1.0";
        rules.HeaderLines = "Piratenpartei Deutschland Landesverband Baden-Württemberg\n"
            + "Landesgeschäftsstelle - Stöckachstr. 53 - 70190 Stuttgart\n"
            + "An den Schatzmeister im Landesvorstand (schatzmeister@piratenpartei-bw.de)";
        rules.CarRates = [new RateStepDTO { UpToKm = 20, RatePerKm = 0.30m }, new RateStepDTO { RatePerKm = 0.35m }];
        rules.SubmissionDeadlineDays = 56;
        rules.CutoffDay = 28;
        rules.CutoffMonth = 2;
        return rules;
    }

    /// <summary>Form J1.1 for trips from 01.03.2022; the motorcycle rate is the one printed on the form.</summary>
    public static TravelExpenseRulesDTO LowerSaxony() {
        var rules = EStGBase();
        rules.ValidFrom = new DateOnly(2022, 3, 1);
        rules.VersionLabel = "J1.1";
        rules.HeaderLines = "Schatzmeister Niedersachsen (schatzmeister@piraten-nds.de)";
        rules.CarRates = [new RateStepDTO { RatePerKm = 0.30m }];
        rules.MotorcycleRates = [new RateStepDTO { RatePerKm = 0.13m }];
        rules.SubmissionDeadlineDays = 56;
        rules.CutoffDay = 28;
        rules.CutoffMonth = 2;
        return rules;
    }

    /// <summary>Meal allowance and reductions after § 9 Abs. 4a EStG, the accommodation flat rate of the rules.</summary>
    private static TravelExpenseRulesDTO EStGBase() {
        return new TravelExpenseRulesDTO {
            TemplateKey = DefaultTemplateKey,
            MealDayTripRate = 14,
            MealArrivalDepartureRate = 14,
            MealFullDayRate = 28,
            BreakfastReductionPercent = 20,
            LunchReductionPercent = 40,
            DinnerReductionPercent = 40,
            AccommodationFlatRate = 20
        };
    }

    private const string FederalRulesText = """
        Reisekostenordnung (Beschlossen am 15.02.2024)
        Rückwirkend gültig ab 01.01.2024. Beschlossen im Redmine Ticket #224606

        A. Erstattungsfähig sind Kosten, die Mitgliedern oder anderen beauftragten Personen der Piratenpartei Deutschland entstehen bei der Wahrnehmung von
        1. Ämtern, in die sie von einem Bundes- oder Landesparteitag oder einem anderen, satzungsgemäß berechtigten Organ der Piratenpartei gewählt wurden, oder
        2. Mandaten, die ihnen von einem Bundes- oder Landesparteitag oder einem anderen, satzungsgemäß berechtigten Organ der Piratenpartei erteilt wurden, oder
        3. Aufgaben, mit denen sie von einem Bundes- oder Landesparteitag oder einem anderen, satzungsgemäß berechtigten Organ der Piratenpartei betraut wurden.

        B. Das Reiseanliegen ist dem Vorsitzenden des betreffenden Gebietsverbands, dessen Stellvertreter, dem zuständigen Schatzmeister, stellvertretender Schatzmeister oder einem für das entsprechende Budget Beauftragten unter Angabe von Ziel und Zweck der Reise vorab anzuzeigen. Abfahrtsort und Rückfahrtziel sind nur dann anzuzeigen, wenn sie vom Wohnort abweichen. Reisekosten sind nur mit Zustimmung der vorbenannten erstattungsfähig. Diese achten auf eine angemessene und wirtschaftliche Ausübung von Dienstreisen.

        C. Vorstände reisen in ihrem Tätigkeitsgebiet selbstständig, ohne vorherige Pflicht zur Anmeldung.

        D. Erstattungen erfolgen nur auf Antrag. Für die Erstattung ist nur das vorliegende Standard-Formular zu verwenden. Für nicht im Formular berücksichtigte Sachverhalte und Belege sind dem Formular Anlagen beizufügen.

        E. Abrechnungen können nur bei den zuständigen Schatzmeistern oder deren Beauftragten eingereicht und von diesen erstattet werden.

        F. Mit Rücksicht auf die Kassenlage werden die erstattungsberechtigten Mitglieder der Piratenpartei Deutschland darum gebeten, den erstattungsfähigen Betrag oder einen Teilbetrag der Piratenpartei als Spende zur Verfügung zu stellen. Die entsprechende Spendenbescheinigung erstellt der jeweilige Schatzmeister oder ein mit Finanzangelegenheiten Beauftragter.

        G. Die Kostenerstattung muss innerhalb von 8 Wochen nach Entstehung der Ansprüche auf dem dafür vorgesehenen Standard-Formular beantragt werden. Pro Reise ist ein Formular einzureichen. Ausnahmen hiervon sind nur mit Zustimmung des zuständigen Schatzmeisters oder dessen Stellvertreter zulässig.

        H. Erstattung von Kosten
        1. Fahrtkosten werden wie folgt erstattet:
        a) Bei Benutzung öffentlicher Verkehrsmittel die nachgewiesenen Kosten entsprechend Beleg/Fahrkarte. Bei Bahnreisen die Kosten in Höhe der Kosten der II. Klasse. Besitzer von Bahncards nutzen bitte ihre Ermäßigungen. Zum Wohle der wirtschaftlichen Lage der Piratenpartei sollten Bahnreisen immer unter Ausschöpfung aller Sparangebote durchgeführt werden. Sofern ein Mitglied sich eine Bahncard angeschafft hat, kann dieses Mitglied bei jeder erforderlichen Reise die fiktiven Kosten einer normalen Bahnfahrkarte der II.Klasse in Anrechnung bringen, bis die Eigenkosten der Bahncard abgerechnet wurden. Die Rechnung für die Bahncard ist im Original an den Schatzmeister zu übergeben. Kosten für Bahncards, die bereits an anderer Stelle steuerlich berücksichtigt werden (beispielsweise vom Arbeitgeber finanziert) können nicht abgerechnet werden. Mitgliedern des Bundesvorstands werden die Kosten für eine Bahncard 50 erstattet.
        b) Die Benutzung öffentlicher Verkehrsmittel ist der Benutzung von PKW/Motorrad vorzuziehen. Wird zur Wahrnehmung der Aufgaben ein eigener, privater PKW benutzt, werden die Kosten gemäß EStG ersetzt. Zum Nachweis ist der Reisekostenabrechnung eine Routenplanung der tatsächlich gefahrenen Strecke beizufügen.
        c) Flugreisen werden nur dann erstattet, wenn aus einer Kostenvergleichsrechnung eine vergleichsweise günstigere Reise gegenüber einer Zugfahrt (zweite Klasse) möglich ist oder wenn es aus Zeitgründen keine Alternative gibt. In diesem Fall ist eine vorherige Genehmigung durch den Vorsitzenden oder Stellvertreter notwendig.
        2. Der Verpflegungsmehraufwand berechnet sich gemäß den Vorgaben des EStG.
        3. Übernachtungsaufwendungen: Die Kostenerstattung erfolgt nach Beleg und die Rechnung muss auf die Piratenpartei ausgestellt sein. Die ausgewiesenen Kosten für die Verpflegung sind abzuziehen. Pauschal können maximal 20,00 Euro abgerechnet werden. Ist die Verpflegung bereits pauschal im Übernachtungspreis enthalten, so wird der Erstattungsbetrag um den Betrag gemäß EStG reduziert. Das entsprechende Verpflegungsentgelt wird bei der Berechnung des Verpflegungsaufwands berücksichtigt.
        4. Sonstige Aufwendungen werden nur gegen Vorlage von Belegen erstattet, wenn sie im ursächlichen Zusammenhang mit der abzurechnenden Tätigkeit stehen. Ohne Belegnachweis werden sonstige Aufwendungen nicht erstattet.

        I. Reisen in Orte außerhalb des Tätigkeitsgebiets des betroffenen Gebietsverbandes und deren Abrechnung, benötigen einen Beschluss des Vorstands des betroffenen Gebietsverbands.

        J. Alle Kostenerstattungen, die nach dem 28. Februar des Folgejahres geltend gemacht werden, sind nicht mehr erstattungsfähig.
        """;

    private const string BavariaRulesText = """
        Reisekostenordnung (Beschlossen am 01.04.2022 durch den LaVo Bayern, gültig für alle Gliederungen im Landesverband Bayern)

        A. Erstattungsfähig sind Kosten, die Mitgliedern oder anderen beauftragten Personen der Piratenpartei Deutschland entstehen bei der Wahrnehmung von
        1. Ämtern, in die sie von einem Bundes- oder Landesparteitag oder einem anderen, satzungsgemäß berechtigten Organ der Piratenpartei gewählt wurden, oder
        2. Mandaten, die ihnen von einem Bundes- oder Landesparteitag oder einem anderen, satzungsgemäß berechtigten Organ der Piratenpartei erteilt wurden, oder
        3. Aufgaben, mit denen sie von einem Bundes- oder Landesparteitag oder einem anderen, satzungsgemäß berechtigten Organ der Piratenpartei betraut wurden.

        B. Das Reiseanliegen ist dem Vorsitzenden des betreffenden Gebietsverbands, dessen Stellvertreter oder dem zuständigen Schatzmeister unter Angabe von Ziel und Zweck der Reise vorab anzuzeigen. Abfahrtsort und Rückfahrtsziel sind nur dann anzuzeigen, wenn sie vom Wohnort abweichen. Reisekosten sind nur mit Zustimmung des Vorsitzenden oder seines Stellvertreters erstattungsfähig. Diese achten auf eine angemessene und wirtschaftliche Ausübung von Dienstreisen.

        C. Erstattungen erfolgen nur auf Antrag. Für die Erstattung ist nur das vorliegende Standard-Formular zu verwenden. Für nicht im Formular berücksichtigte Sachverhalte und Belege sind dem Formular Anlagen beizufügen.

        D. Abrechnungen können nur bei den zuständigen Schatzmeistern eingereicht und von diesen erstattet werden.

        E. Mit Rücksicht auf die Kassenlage werden die erstattungsberechtigten Mitglieder der Piratenpartei Deutschland darum gebeten, den erstattungsfähigen Betrag oder einen Teilbetrag der Piratenpartei als Spende zur Verfügung zu stellen. Die entsprechende Spendenbescheinigung erstellt der jeweilige Schatzmeister oder ein mit Finanzangelegenheiten Beauftragter.

        F. Die Kostenerstattung sollte grundsätzlich innerhalb eines Monats nach Entstehung der Ansprüche auf dem dafür vorgesehenen Standard-Formular beantragt werden. Pro Reise ist ein Formular einzureichen. Ausnahmen hiervon sind nur mit Zustimmung des zuständigen Schatzmeisters zulässig.

        G. Erstattung von Kosten
        1. Fahrtkosten werden wie folgt erstattet:
        a) Bei Benutzung öffentlicher Verkehrsmittel die nachgewiesenen Kosten entsprechend Beleg/Fahrkarte. Bei Bahnreisen die Kosten in Höhe der Kosten der II. Klasse. Besitzer von Bahncards nutzen bitte ihre Ermäßigungen. Zum Wohle der wirtschaftlichen Lage der Piratenpartei sollten Bahnreisen immer unter Ausschöpfung aller Sparangebote durchgeführt werden. Sofern ein Mitglied sich eine Bahncard angeschafft hat, kann dieses Mitglied bei jeder erforderlichen Reise die fiktiven Kosten einer normalen Bahnfahrkarte der II.Klasse in Anrechnung bringen, bis die Eigenkosten der Bahncard abgerechnet wurden. Die Rechnung für die Bahncard ist im Original an den Schatzmeister zu übergeben. Kosten für Bahncards, die bereits an anderer Stelle steuerlich berücksichtigt werden (beispielsweise vom Arbeitgeber finanziert) können nicht abgerechnet werden. Mitgliedern des Landesvorstands werden die Kosten für eine Bahncard 50 erstattet.
        b) Die Benutzung öffentlicher Verkehrsmittel ist der Benutzung von PKW vorzuziehen. Wird zur Wahrnehmung der Aufgaben ein eigener, privater PKW benutzt, so beträgt die Erstattungspauschale 0,30 Euro pro gefahrenen Kilometer. Zum Nachweis ist der Reisekostenabrechnung eine Routenplanung der tatsächlich gefahrenen Strecke beizufügen.
        c) Bei Benutzung eines Motorrades werden 0,13 Euro/km erstattet. Die Regelungen des Absatzes b) gelten entsprechend.
        d) Flugreisen werden nur dann erstattet, wenn aus einer Kostenvergleichsrechnung eine vergleichsweise günstigere Reise gegenüber einer Zugfahrt (zweite Klasse) möglich ist oder wenn es aus Zeitgründen keine Alternative gibt. In diesem Fall ist eine vorherige Genehmigung durch den Vorsitzenden oder Stellvertreter notwendig.
        2. Der Verpflegungsmehraufwand beträgt:
        a) bei eintägiger Reise und einer Mindestabwesenheit von >8 Stunden 14 Euro
        b) bei mehrtägigen Reisen am An- und Abreisetag jeweils 14 Euro und an den Zwischentagen jeweils 28 Euro
        3. Übernachtungsaufwendungen: Die Kostenerstattung erfolgt nach Beleg und die Rechnung muss auf die Piratenpartei ausgestellt sein. Die ausgewiesenen Kosten für das Frühstück sind abzuziehen. Pauschal können maximal 20,00 Euro abgerechnet werden. Ist das Frühstück bereits pauschal im Übernachtungspreis enthalten, so wird der Erstattungsbetrag um 5,60 Euro reduziert. Das entsprechende Frühstücksentgelt wird bei der Berechnung des Verpflegungsaufwands berücksichtigt.
        4. Sonstige Aufwenden werden nur gegen Vorlage von Belegen erstattet, wenn sie im ursächlichen Zusammenhang mit der abzurechnenden Tätigkeit stehen. Ohne Belegnachweis werden sonstige Aufwendungen nicht erstattet.

        H. Reisen in Orte außerhalb des Tätigkeitsgebiets des betroffenen Gebietsverbandes und deren Abrechnung, benötigen einen Beschluss des Vorstands des betroffenen Gebietsverbands.

        I. Alle Kostenerstattungen, die nach dem 15. Februar des Folgejahres geltend gemacht werden, sind nicht mehr erstattungsfähig.
        """;
}
