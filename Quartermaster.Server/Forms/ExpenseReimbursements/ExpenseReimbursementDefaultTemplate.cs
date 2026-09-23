namespace Quartermaster.Server.Forms.ExpenseReimbursements;

internal static class ExpenseReimbursementDefaultTemplate {
    public const string Body = @"<page size=""A4"" margin=""36"" font-family=""Helvetica"" font-size=""10"">
    <row>
        <div>
            <column>
                <p>
                    <span size=""20"" bold=""true"">Bestätigung von Auslagen</span><span size=""8""> (Version 1.02)</span>
                </p>
                <p size=""13"" color=""grey-darken-2"" pad-top=""2"">{{ form.ChapterName | default: ""Name der Gliederung"" }}</p>
            </column>
        </div>
        <div width=""140"">
            <svg path=""assets/pp-logo.svg"" width=""140"" />
        </div>
    </row>

    <spacer height=""4"" />
    <p>Hiermit bestätige ich</p>

    <column spacing=""2"">
        <row>
            <div width=""60"">
                <p color=""grey-darken-2"">Name</p>
            </div>
            <div border-bottom=""0.5"">
                <p>{{ form.FirstName }} {{ form.LastName }}</p>
            </div>
            <div width=""20"" />
            <div width=""80"">
                <p color=""grey-darken-2"">Mitglieds-Nr.</p>
            </div>
            <div width=""80"" border-bottom=""0.5"">
                <p>{{ form.MemberNumber }}</p>
            </div>
        </row>
        <row>
            <div width=""60"">
                <p color=""grey-darken-2"">Adresse</p>
            </div>
            <div border-bottom=""0.5"">
                <p>{{ form.Street }}</p>
            </div>
        </row>
        <row>
            <div width=""60"" />
            <div border-bottom=""0.5"">
                <p>{{ form.PostCode }} {{ form.City }}</p>
            </div>
        </row>
    </column>

    <row pad-top=""8"">
        <div width=""60"">
            <p color=""grey-darken-2"">Funktion</p>
        </div>
        <div border-bottom=""0.5"">
            <p>{{ form.Function }}</p>
        </div>
        <div width=""20"" />
        <div width=""80"">
            <p color=""grey-darken-2"">Beschluss vom</p>
        </div>
        <div width=""80"" border-bottom=""0.5"">
            <p>{{ form.MotionDate | german_date }}</p>
        </div>
    </row>

    <hr thickness=""1.5"" pad-top=""6"" />

    <p>Folgende Leistung(en) für die Piratenpartei Deutschland erbracht zu haben:</p>

    <table columnDefinitions=""4,2,80pt"">
        <thead>
            <tr>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""4"">Erbrachte Leistung / Anschaffung</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""4"">Verbuchung*</th>
                <th background=""grey-lighten-3"" bold=""true"" pad-vertical=""4"" align=""right"">Wert</th>
            </tr>
        </thead>
        <tbody>
            {% for item in form.LineItems %}
                <tr>
                    <td>{{ item.Description }}</td>
                    <td> </td>
                    <td align=""right"">{{ item.Value | money }}</td>
                </tr>
            {% endfor %}
        </tbody>
        <tfoot>
            <tr>
                <td colspan=""2"" pad-vertical=""4"">
                    <span size=""8"" italic=""true"">*wird vom Schatzmeister ausgefüllt.</span><span> </span><span bold=""true"">Summe ∑</span>
                </td>
                <td pad-vertical=""4"" align=""right"" bold=""true"">{{ form.LineItems | sum_by: ""Value"" | money }}</td>
            </tr>
        </tfoot>
    </table>

    <column spacing=""2"" pad-top=""6"">
        <row spacing=""4"">
            <div width=""16"">
                <input type=""checkbox"" checked=""{% if form.DonatedAmount > 0 %}true{% else %}false{% endif %}"" />
            </div>
            <p>Ich verzichte auf Auszahlung von <span bold=""true"">{% if form.DonatedAmount > 0 %}{{ form.DonatedAmount | amount }}{% else %}________{% endif %}</span> Euro und spende diesen Betrag stattdessen.</p>
        </row>
        <row spacing=""4"">
            <div width=""16"">
                <input type=""checkbox"" checked=""{% if form.DonatedAmount > 0 and form.RequestsDonationReceipt %}true{% else %}false{% endif %}"" />
            </div>
            <p>Ich bitte um die Ausstellung einer Zuwendungsbescheinigung.</p>
        </row>
        <row spacing=""4"">
            <div width=""16"">
                <input type=""checkbox"" checked=""{% if form.PayoutAmount > 0 %}true{% else %}false{% endif %}"" />
            </div>
            <p>Ich bitte um Auszahlung von <span bold=""true"">{% if form.PayoutAmount > 0 %}{{ form.PayoutAmount | amount }}{% else %}________{% endif %}</span> Euro.</p>
        </row>
        <row spacing=""4"">
            <div width=""16"">
                <input type=""checkbox"" checked=""{% if form.PayoutAmount > 0 and form.PayoutMethod == ""BankTransfer"" %}true{% else %}false{% endif %}"" />
            </div>
            <p>Ich bitte um die Auszahlung des Betrags per Banküberweisung auf folgendes Konto</p>
        </row>
        <row pad-left=""20"">
            <div width=""40"">
                <p color=""grey-darken-2"">IBAN</p>
            </div>
            <div border-bottom=""0.5"">
                <p>{{ form.Iban }}</p>
            </div>
        </row>
        <row pad-left=""20"">
            <div width=""40"">
                <p color=""grey-darken-2"">BIC</p>
            </div>
            <div border-bottom=""0.5"">
                <p>{{ form.Iban | bic_lookup }}</p>
            </div>
        </row>
        <row spacing=""4"">
            <div width=""16"">
                <input type=""checkbox"" checked=""{% if form.PayoutAmount > 0 and form.PayoutMethod == ""Cash"" %}true{% else %}false{% endif %}"" />
            </div>
            <p>Ich bitte um Barauszahlung.</p>
        </row>
    </column>

    <row pad-top=""12"">
        <div>
            <column>
                <p>{% if form.SignatureLocation != """" %}{{ form.SignatureLocation }}, {% endif %}{{ form.FormDate | german_date }}</p>
                <div border-top=""0.5"" pad-top=""2"">
                    <p size=""8"" color=""grey-darken-2"">Ort, Datum</p>
                </div>
            </column>
        </div>
        <div width=""20"" />
        <div>
            <column>
                <p> </p>
                <div border-top=""0.5"" pad-top=""2"">
                    <p size=""8"" color=""grey-darken-2"">Unterschrift des Mitglieds</p>
                </div>
            </column>
        </div>
    </row>

    <p size=""9"" pad-top=""6"">
        <span>Hinweis: Es können nur Auslagen eingereicht werden, welche </span><span bold=""true"">vorher</span><span> auf Beschluss des Vorstands genehmigt wurden. Belege müssen immer im Original zusammen mit diesem Formular an den Schatzmeister übergegeben werden. </span><span bold=""true"">Ohne Originalbelege kann keine Erstattung erfolgen.</span>
    </p>

    {% if form.PayoutAmount > 0 and form.PayoutMethod == ""Cash"" %}
        <column pad-top=""12"">
            <p size=""9"">Bei Barauszahlung: Betrag erhalten am </p>
            <row pad-top=""8"">
                <div>
                    <column>
                        <p> </p>
                        <div border-top=""0.5"" pad-top=""2"">
                            <p size=""8"" color=""grey-darken-2"">Ort, Datum</p>
                        </div>
                    </column>
                </div>
                <div width=""20"" />
                <div>
                    <column>
                        <p> </p>
                        <div border-top=""0.5"" pad-top=""2"">
                            <p size=""8"" color=""grey-darken-2"">Unterschrift des Mitglieds</p>
                        </div>
                    </column>
                </div>
            </row>
        </column>
    {% endif %}

    <row pad-top=""12"">
        <div>
            <div border=""0.5"" padding=""6"" min-height=""70"">
                <p size=""9"" color=""grey-darken-1"">Geschäftsstelle:</p>
            </div>
        </div>
        <div width=""8"" />
        <div>
            <div border=""0.5"" padding=""6"" min-height=""70"">
                <p size=""9"" color=""grey-darken-1"">Bankverbindung:</p>
            </div>
        </div>
        <div width=""8"" />
        <div>
            <div border=""0.5"" padding=""6"" min-height=""70"">
                <p size=""9"" color=""grey-darken-1"">Vorstand:</p>
            </div>
        </div>
    </row>
</page>
";
}
