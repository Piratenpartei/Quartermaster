namespace Quartermaster.Server.TravelExpenses;

/// <summary>
/// The official "Antrag auf Erstattung von Reisekosten" layout (federal J2.0, also used by Bayern, Baden-Württemberg and
/// Niedersachsen), filled from <see cref="Api.TravelExpenses.TravelExpensePdfModel"/>: the form, the rules text and an
/// appendix with the trip and every calculation the fixed form lines can't show.
/// </summary>
internal static class TravelExpenseDefaultTemplate {
    public const string Body = @"<document>
<page size=""A4"" margin=""32"" font-family=""Helvetica"" font-size=""9"">
    <row>
        <div>
            <column>
                <p><span size=""15"" bold=""true"">Antrag auf Erstattung von Reisekosten</span><span size=""8""> (Version {{ form.VersionLabel }})</span></p>
                <p size=""8"">Für Reisen ab dem {{ form.ValidFrom }}</p>
                {% for line in form.HeaderLines %}<p size=""8"" color=""grey-darken-2"">{{ line }}</p>{% endfor %}
            </column>
        </div>
        <div width=""110"">
            <svg path=""assets/pp-logo.svg"" width=""110"" />
        </div>
    </row>

    <p bold=""true"" pad-top=""8"">1. Antragsteller</p>
    <column spacing=""2"">
        <row>
            <div width=""70""><p color=""grey-darken-2"">Name</p></div>
            <div border-bottom=""0.5""><p>{{ form.Name }}</p></div>
            <div width=""12"" />
            <div width=""60""><p color=""grey-darken-2"">Mitglieds-Nr.</p></div>
            <div width=""80"" border-bottom=""0.5""><p>{{ form.MemberNumber }}</p></div>
        </row>
        <row>
            <div width=""70""><p color=""grey-darken-2"">Adresse</p></div>
            <div border-bottom=""0.5""><p>{{ form.Street }}</p></div>
        </row>
        <row>
            <div width=""70"" />
            <div border-bottom=""0.5""><p>{{ form.PostCodeCity }}</p></div>
        </row>
        <row>
            <div width=""70""><p color=""grey-darken-2"">E-Mail (optional)</p></div>
            <div border-bottom=""0.5""><p>{{ form.Email }}</p></div>
        </row>
        <row>
            <div width=""70""><p color=""grey-darken-2"">Funktion</p></div>
            <div border-bottom=""0.5""><p>{{ form.Function }}</p></div>
            <div width=""12"" />
            <div width=""60""><p color=""grey-darken-2"">LV</p></div>
            <div width=""80"" border-bottom=""0.5""><p>{{ form.MembershipChapter }}</p></div>
        </row>
    </column>

    <p bold=""true"" pad-top=""8"">2. Zweck der Reise</p>
    <column spacing=""2"">
        <row>
            <div width=""70""><p color=""grey-darken-2"">Zweck</p></div>
            <div border-bottom=""0.5""><p>{{ form.Purpose }}</p></div>
        </row>
        <row>
            <div width=""70""><p color=""grey-darken-2"">Beschluss-Nr.</p></div>
            <div border-bottom=""0.5""><p>{{ form.ResolutionNumber }}</p></div>
            <div width=""12"" />
            <div width=""30""><p color=""grey-darken-2"">vom</p></div>
            <div width=""80"" border-bottom=""0.5""><p>{{ form.ResolutionDate }}</p></div>
        </row>
    </column>

    <p bold=""true"" pad-top=""8"">3. Dauer und Ziel der Reise</p>
    <column spacing=""2"">
        <row>
            <div width=""70""><p color=""grey-darken-2"">Dauer: vom</p></div>
            <div width=""80"" border-bottom=""0.5""><p>{{ form.StartDate }}</p></div>
            <div width=""24""><p color=""grey-darken-2"" align=""center"">bis</p></div>
            <div width=""80"" border-bottom=""0.5""><p>{{ form.EndDate }}</p></div>
            <div width=""12"" />
            <div width=""30""><p color=""grey-darken-2"">Ziel:</p></div>
            <div border-bottom=""0.5""><p>{{ form.Destination }}</p></div>
        </row>
        <row>
            <div width=""110""><p color=""grey-darken-2"">Hinfahrt: Abfahrtszeit</p></div>
            <div width=""60"" border-bottom=""0.5""><p>{{ form.StartTime }}</p></div>
            <div width=""24""><p color=""grey-darken-2""> Uhr</p></div>
            <div width=""24"" />
            <div width=""120""><p color=""grey-darken-2"">Rückfahrt: Ankunftszeit</p></div>
            <div width=""60"" border-bottom=""0.5""><p>{{ form.EndTime }}</p></div>
            <div width=""24""><p color=""grey-darken-2""> Uhr</p></div>
        </row>
    </column>

    <p bold=""true"" pad-top=""8"">4. Fahrtkosten</p>
    <p size=""7.5"">Erstattungsfähig sind 2. Klasse Tickets mit Bahn, Bus oder Flugzeug. Bei Fahrten mit dem eigenen PKW können {{ form.CarRate }} pro Kilometer erstattet werden{% if form.MotorcycleRate != """" %}, mit dem Motorrad {{ form.MotorcycleRate }}{% endif %}. (bitte Beleg oder Routenplan anfügen)</p>
    <column spacing=""2"">
        <row>
            <div><p>Ticket 2. Klasse</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ form.Tickets }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
        <row>
            <div width=""180""><p>Fahrtkosten laut beigefügtem Routenplan</p></div>
            <div width=""60"" border-bottom=""0.5""><p align=""right"">{{ form.MileageKm }}</p></div>
            <div><p> km x {{ form.MileageRate }}</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ form.Mileage }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
    </column>

    <p bold=""true"" pad-top=""8"">5. Verpflegungsmehraufwand</p>
    <p size=""7.5"">Die erstattungsfähigen Pauschalen sind: Bei eintägigen Reisen über 8 Stunden {{ form.DayTripRate }}, bei mehrtägigen Reisen: An/Abreisetag jeweils {{ form.ArrivalDepartureRate }} / ganze Tage = {{ form.FullDayRate }}</p>
    <column spacing=""2"">
        <row>
            <div width=""120""><p>1. An/Abreisetage:</p></div>
            <div width=""30"" border-bottom=""0.5""><p align=""right"">{{ form.ArrivalDepartureDays }}</p></div>
            <div width=""80""><p> Tage x {{ form.ArrivalDepartureRate }}</p></div>
            <div width=""70"" border-bottom=""0.5""><p align=""right"">{{ form.ArrivalDepartureAmount }}</p></div>
            <div width=""14""><p> €</p></div>
            <div />
        </row>
        <row>
            <div width=""120""><p>2. volle Aufenthaltstage:</p></div>
            <div width=""30"" border-bottom=""0.5""><p align=""right"">{{ form.FullDays }}</p></div>
            <div width=""80""><p> Tage x {{ form.FullDayRate }}</p></div>
            <div width=""70"" border-bottom=""0.5""><p align=""right"">{{ form.FullDaysAmount }}</p></div>
            <div width=""14""><p> €</p></div>
            <div />
        </row>
        {% if form.MealReductions != """" %}
        <row>
            <div width=""230""><p>abzüglich gestellter Mahlzeiten (siehe Anlage)</p></div>
            <div width=""70"" border-bottom=""0.5""><p align=""right"">{{ form.MealReductions }}</p></div>
            <div width=""14""><p> €</p></div>
            <div />
        </row>
        {% endif %}
        <row>
            <div><p align=""right"">Summe</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ form.MealAllowance }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
    </column>

    <p bold=""true"" pad-top=""8"">6. Übernachtungskosten</p>
    <column spacing=""2"">
        <row>
            <div><p>laut Beleg (abzüglich laut Beleg angefallener Frühstückskosten)</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ form.AccommodationReceipt }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
        <row>
            <div><p>pauschal {{ form.AccommodationFlatRate }} pro Übernachtung{% if form.AccommodationFlatNights > 0 %} ({{ form.AccommodationFlatNights }} Nächte){% endif %}</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ form.AccommodationFlat }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
    </column>

    <p bold=""true"" pad-top=""8"">7. sonstige Kosten</p>
    <p size=""7.5"">Sonstige Aufwendungen (Eintrittsgelder, ÖPNV Tickets, Taxikosten, Mietwagen &amp; Benzinkosten) werden nur gegen Vorlage von Belegen erstattet, die im ursächlichen Zusammenhang mit der abzurechnenden Tätigkeit stehen. Ohne Beleg keine Erstattung.</p>
    <column spacing=""2"">
        {% for cost in form.OtherCosts %}
        <row>
            <div border-bottom=""0.5""><p>{{ cost.Description }}</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ cost.Amount }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
        {% endfor %}
        {% if form.OtherCosts.size == 0 %}
        <row>
            <div border-bottom=""0.5""><p> </p></div>
            <div width=""80"" border-bottom=""0.5""><p> </p></div>
            <div width=""14""><p> €</p></div>
        </row>
        {% endif %}
    </column>

    <column spacing=""2"" pad-top=""8"">
        <row>
            <div><p bold=""true"">Summe der erstattungsfähigen Kosten</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"" bold=""true"">{{ form.Total }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
        <row>
            <div><p>davon spende ich an die Piratenpartei Deutschland</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"">{{ form.Donation }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
        <row>
            <div><p bold=""true"">Zahlbetrag</p></div>
            <div width=""80"" border-bottom=""0.5""><p align=""right"" bold=""true"">{{ form.Payout }}</p></div>
            <div width=""14""><p> €</p></div>
        </row>
    </column>

    <p pad-top=""8"">Bitte überweist den Zahlbetrag auf folgendes Konto:</p>
    <column spacing=""2"">
        <row>
            <div width=""40""><p color=""grey-darken-2"">IBAN:</p></div>
            <div border-bottom=""0.5""><p>{{ form.Iban }}</p></div>
        </row>
        <row>
            <div width=""40""><p color=""grey-darken-2"">BIC:</p></div>
            <div border-bottom=""0.5""><p>{% if form.Bic != """" %}{{ form.Bic }}{% else %}{{ form.Iban | bic_lookup }}{% endif %}</p></div>
        </row>
    </column>

    <row pad-top=""8"">
        <div width=""110""><p>Anzahl der Anlagen:</p></div>
        <div width=""40"" border-bottom=""0.5""><p>{{ form.AttachmentCount }}</p></div>
        <div />
    </row>

    <row pad-top=""14"">
        <div>
            <column>
                <p>{{ form.FormDate }}</p>
                <div border-top=""0.5"" pad-top=""2""><p size=""7.5"" color=""grey-darken-2"">Datum</p></div>
            </column>
        </div>
        <div width=""24"" />
        <div>
            <column>
                <p> </p>
                <div border-top=""0.5"" pad-top=""2""><p size=""7.5"" color=""grey-darken-2"">Unterschrift</p></div>
            </column>
        </div>
    </row>

    <column pad-top=""10"">
    <div border=""0.5"" padding=""4"">
        <column spacing=""2"">
            <p size=""7.5"" bold=""true"">Bearbeitungsvermerke</p>
            <row>
                <div><p size=""7.5"">Datum</p></div>
                <div><p size=""7.5"">Soll-Konto</p></div>
            </row>
            <row>
                <div><p size=""7.5"">Beleg-Nr.:</p></div>
                <div><p size=""7.5"">Haben-Konto</p></div>
            </row>
            <row>
                <div><p size=""7.5"">Unterschrift Bearbeiter</p></div>
                <div><p size=""7.5"">Konto</p></div>
            </row>
        </column>
    </div>
    </column>
</page>
{% if form.RulesParagraphs.size > 0 %}
<page size=""A4"" margin=""32"" font-family=""Helvetica"" font-size=""8"">
    <column spacing=""3"">
        {% for paragraph in form.RulesParagraphs %}<p>{{ paragraph }}</p>{% endfor %}
    </column>
</page>
{% endif %}
<page size=""A4"" margin=""32"" font-family=""Helvetica"" font-size=""9"">
    <p size=""13"" bold=""true"">Anlage: Reiseverlauf und Berechnung</p>
    <p color=""grey-darken-2"">{{ form.Name }} · {{ form.Purpose }}{% if form.TravelChapter != """" %} · {{ form.TravelChapter }}{% endif %}</p>

    <p bold=""true"" pad-top=""8"">Reiseverlauf</p>
    <table columnDefinitions=""90pt,3,4,70pt"">
        <thead>
            <tr>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"">Zeit</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"">Abschnitt</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"">Angaben</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"" align=""right"">Betrag</th>
            </tr>
        </thead>
        <tbody>
            {% for row in form.Timeline %}
            <tr>
                <td>{{ row.When }}</td>
                <td>{{ row.Title }}</td>
                <td>{{ row.Detail }}</td>
                <td align=""right"">{{ row.Amount }}</td>
            </tr>
            {% endfor %}
        </tbody>
    </table>

    {% if form.MealDays.size > 0 %}
    <p bold=""true"" pad-top=""10"">Verpflegungsmehraufwand nach Tagen</p>
    <table columnDefinitions=""110pt,3,60pt,60pt,60pt"">
        <thead>
            <tr>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"">Tag</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"">Art</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"" align=""right"">Pauschale</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"" align=""right"">Mahlzeiten</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""3"" align=""right"">Betrag</th>
            </tr>
        </thead>
        <tbody>
            {% for day in form.MealDays %}
            <tr>
                <td>{{ day.Date }}</td>
                <td>{{ day.Kind }}</td>
                <td align=""right"">{{ day.Allowance }}</td>
                <td align=""right"">{{ day.Reduction }}</td>
                <td align=""right"">{{ day.Net }}</td>
            </tr>
            {% endfor %}
        </tbody>
    </table>
    {% endif %}

    {% if form.AllOtherCosts.size > 0 %}
    <p bold=""true"" pad-top=""10"">Sonstige Kosten</p>
    <table columnDefinitions=""5,70pt"">
        <tbody>
            {% for cost in form.AllOtherCosts %}
            <tr>
                <td>{{ cost.Description }}</td>
                <td align=""right"">{{ cost.Amount }} €</td>
            </tr>
            {% endfor %}
        </tbody>
    </table>
    {% endif %}

    {% if form.Notes.size > 0 %}
    <p bold=""true"" pad-top=""10"">Hinweise</p>
    <column spacing=""2"">
        {% for note in form.Notes %}<p><span bold=""true"">{{ note.Description }}:</span><span> {{ note.Amount }}</span></p>{% endfor %}
    </column>
    {% endif %}
</page>
</document>";
}
