using FluentMigrator;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Templates;

namespace Quartermaster.Data.Migrations;

[Migration(017)]
public class M009 : MigrationBase {
    public const string ApplicationTicketIdentifier = "forwarding.redmine.application";
    public const string ReductionTicketIdentifier = "forwarding.redmine.reduction";

    public const string CityTag = "{{ application.City }}";
    public const string CountryTag = "{% if application.Country != \"\" %}, {{ application.Country }}{% endif %}";
    public const string OtherPartyYes = "{% if application.IsMemberOfAnotherParty %}ja{% else %}";
    public const string OtherPartyYesWithNames = "{% if application.IsMemberOfAnotherParty %}ja ({{ application.OtherParties }}){% else %}";
    public const string JustificationTag = "{{ dues.Justification }}";
    public const string ApplicationRejectionAmount = ", bei Ablehnung {{ dues.RejectedReductionAmount }} €";
    public const string ReductionRejectionLine = "- Jahresbeitrag bei Ablehnung: {{ dues.RejectedReductionAmount }} €";

    public override void Up() {
        Alter.Table(MembershipApplication.TableName)
            .AddColumn(nameof(MembershipApplication.AddressCountry)).AsString(MembershipApplication.AddressCountryMaxLength).Nullable()
            .AddColumn(nameof(MembershipApplication.OtherParties)).AsString(MembershipApplication.OtherPartiesMaxLength).Nullable();

        // Seeds are only inserted once, so the system templates and the chapter copies get the new lines here.
        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(REPLACE(REPLACE(`{nameof(Template.Body)}`,
                '{CityTag}', '{CityTag}{CountryTag}'),
                '{OtherPartyYes}', '{OtherPartyYesWithNames}'),
                '{JustificationTag}', '{JustificationTag}{ApplicationRejectionAmount}')
            WHERE `{nameof(Template.Identifier)}` = '{ApplicationTicketIdentifier}' AND `{nameof(Template.Body)}` NOT LIKE '%RejectedReductionAmount%'");

        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(`{nameof(Template.Body)}`,
                '{JustificationTag}', CONCAT('{JustificationTag}', CHAR(10), '{ReductionRejectionLine}'))
            WHERE `{nameof(Template.Identifier)}` = '{ReductionTicketIdentifier}' AND `{nameof(Template.Body)}` NOT LIKE '%RejectedReductionAmount%'");
    }

    public override void Down() {
        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(REPLACE(REPLACE(`{nameof(Template.Body)}`,
                '{CountryTag}', ''),
                '{OtherPartyYesWithNames}', '{OtherPartyYes}'),
                '{ApplicationRejectionAmount}', '')
            WHERE `{nameof(Template.Identifier)}` = '{ApplicationTicketIdentifier}'");

        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(`{nameof(Template.Body)}`,
                CONCAT(CHAR(10), '{ReductionRejectionLine}'), '')
            WHERE `{nameof(Template.Identifier)}` = '{ReductionTicketIdentifier}'");

        Delete.Column(nameof(MembershipApplication.AddressCountry)).FromTable(MembershipApplication.TableName);
        Delete.Column(nameof(MembershipApplication.OtherParties)).FromTable(MembershipApplication.TableName);
    }
}
