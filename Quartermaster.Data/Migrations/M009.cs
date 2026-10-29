using FluentMigrator;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Templates;

namespace Quartermaster.Data.Migrations;

[Migration(017)]
public class M009 : MigrationBase {
    public const string ApplicationTicketIdentifier = "forwarding.redmine.application";
    public const string ReductionTicketIdentifier = "forwarding.redmine.reduction";

    public const string FailureMailIdentifier = "forwarding.failure.email";
    public const string FailureLegacyPhrase = "nicht angelegt werden";
    public const string FailureActionPhrase = "nicht {{ failure.Action }} werden";
    public const string FailureLegacyRemedy = "Bitte das Ticket von Hand anlegen und den Vorgang danach unter {{ failure.CasesUrl }} als erledigt markieren, oder die Ursache beheben und dort erneut versuchen.";
    public const string FailureRemedyTag = "{{ failure.Remedy }}";

    public const string CityTag = "{{ application.City }}";
    public const string CountryTag = "{% if application.Country != \"\" %}, {{ application.Country }}{% endif %}";
    public const string OtherPartyYes = "{% if application.IsMemberOfAnotherParty %}ja{% else %}";
    public const string OtherPartyYesWithNames = "{% if application.IsMemberOfAnotherParty %}ja ({{ application.OtherParties }}){% else %}";
    public const string JustificationTag = "{{ dues.Justification }}";
    public const string ApplicationRejectionAmount = ", bei Ablehnung {{ dues.RejectedReductionAmount }} €";
    public const string ReductionRejectionLine = "- Jahresbeitrag bei Ablehnung: {{ dues.RejectedReductionAmount }} €";

    public override void Up() {
        Alter.Table(ForwardingTarget.TableName)
            .AddColumn(nameof(ForwardingTarget.SageAssigneeId)).AsInt32().Nullable()
            .AddColumn(nameof(ForwardingTarget.SageDoneStatusId)).AsInt32().Nullable()
            .AddColumn(nameof(ForwardingTarget.SageAwaitReassignment)).AsBoolean().NotNullable().WithDefaultValue(false);

        Alter.Table(MembershipApplication.TableName)
            .AddColumn(nameof(MembershipApplication.AddressCountry)).AsString(MembershipApplication.AddressCountryMaxLength).Nullable()
            .AddColumn(nameof(MembershipApplication.OtherParties)).AsString(MembershipApplication.OtherPartiesMaxLength).Nullable();

        Alter.Table(Chapter.TableName)
            .AddColumn(nameof(Chapter.ContactEmail)).AsString(Chapter.ContactEmailMaxLength).Nullable();

        Alter.Table(Chapter.TableName)
            .AddColumn(nameof(Chapter.NoMemberAdministration)).AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn(nameof(Chapter.UnableToAct)).AsBoolean().NotNullable().WithDefaultValue(false);

        Alter.Table(Motion.TableName)
            .AddColumn(nameof(Motion.OnBehalfOfChapterId)).AsGuid().Nullable()
            .AddColumn(nameof(Motion.OnBehalfOfReason)).AsInt32().Nullable()
            .AddColumn(nameof(Motion.HandedOverToMotionId)).AsGuid().Nullable();

        Alter.Table(MembershipApplication.TableName)
            .AddColumn(nameof(MembershipApplication.RequiresFederalApproval)).AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn(nameof(MembershipApplication.FederalApprovalMotionId)).AsGuid().Nullable();

        Create.Table(ApplicantRegisterEntry.TableName)
            .WithColumn(nameof(ApplicantRegisterEntry.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ApplicantRegisterEntry.Kind)).AsInt32().NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.FirstName)).AsString(ApplicantRegisterEntry.NameMaxLength).NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.LastName)).AsString(ApplicantRegisterEntry.NameMaxLength).NotNullable().Indexed()
            .WithColumn(nameof(ApplicantRegisterEntry.DateOfBirth)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(ApplicantRegisterEntry.Street)).AsString(ApplicantRegisterEntry.StreetMaxLength).NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.HouseNumber)).AsString(ApplicantRegisterEntry.HouseNumberMaxLength).NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.PostCode)).AsString(ApplicantRegisterEntry.PostCodeMaxLength).NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.City)).AsString(ApplicantRegisterEntry.CityMaxLength).NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.Email)).AsString(ApplicantRegisterEntry.EmailMaxLength).Nullable()
            .WithColumn(nameof(ApplicantRegisterEntry.OccurredOn)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.Note)).AsString(ApplicantRegisterEntry.NoteMaxLength).Nullable()
            .WithColumn(nameof(ApplicantRegisterEntry.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ApplicantRegisterEntry.SourceApplicationId)).AsGuid().Nullable()
            .WithColumn(nameof(ApplicantRegisterEntry.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ApplicantRegisterEntry.CreatedByUserId)).AsGuid().Nullable();

        Create.Table(ApplicationRegisterHit.TableName)
            .WithColumn(nameof(ApplicationRegisterHit.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ApplicationRegisterHit.ApplicationId)).AsGuid().NotNullable().Indexed()
            .WithColumn(nameof(ApplicationRegisterHit.EntryId)).AsGuid().NotNullable().Indexed()
            .WithColumn(nameof(ApplicationRegisterHit.MatchedFields)).AsInt32().NotNullable()
            .WithColumn(nameof(ApplicationRegisterHit.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ApplicationRegisterHit.DismissedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(ApplicationRegisterHit.DismissedByUserId)).AsGuid().Nullable();

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

        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(REPLACE(`{nameof(Template.Body)}`,
                '{FailureLegacyPhrase}', '{FailureActionPhrase}'),
                '{FailureLegacyRemedy}', '{FailureRemedyTag}')
            WHERE `{nameof(Template.Identifier)}` = '{FailureMailIdentifier}' AND `{nameof(Template.Body)}` NOT LIKE '%failure.Remedy%'");
    }

    public override void Down() {
        Delete.Table(ApplicationRegisterHit.TableName);
        Delete.Table(ApplicantRegisterEntry.TableName);
        Delete.Column(nameof(MembershipApplication.RequiresFederalApproval)).FromTable(MembershipApplication.TableName);
        Delete.Column(nameof(MembershipApplication.FederalApprovalMotionId)).FromTable(MembershipApplication.TableName);

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

        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(REPLACE(`{nameof(Template.Body)}`,
                '{FailureRemedyTag}', '{FailureLegacyRemedy}'),
                '{FailureActionPhrase}', '{FailureLegacyPhrase}')
            WHERE `{nameof(Template.Identifier)}` = '{FailureMailIdentifier}'");

        Delete.Column(nameof(ForwardingTarget.SageAssigneeId)).FromTable(ForwardingTarget.TableName);
        Delete.Column(nameof(ForwardingTarget.SageDoneStatusId)).FromTable(ForwardingTarget.TableName);
        Delete.Column(nameof(ForwardingTarget.SageAwaitReassignment)).FromTable(ForwardingTarget.TableName);
        Delete.Column(nameof(MembershipApplication.AddressCountry)).FromTable(MembershipApplication.TableName);
        Delete.Column(nameof(MembershipApplication.OtherParties)).FromTable(MembershipApplication.TableName);
        Delete.Column(nameof(Chapter.ContactEmail)).FromTable(Chapter.TableName);
        Delete.Column(nameof(Chapter.NoMemberAdministration)).FromTable(Chapter.TableName);
        Delete.Column(nameof(Chapter.UnableToAct)).FromTable(Chapter.TableName);
        Delete.Column(nameof(Motion.OnBehalfOfChapterId)).FromTable(Motion.TableName);
        Delete.Column(nameof(Motion.OnBehalfOfReason)).FromTable(Motion.TableName);
        Delete.Column(nameof(Motion.HandedOverToMotionId)).FromTable(Motion.TableName);
    }
}
