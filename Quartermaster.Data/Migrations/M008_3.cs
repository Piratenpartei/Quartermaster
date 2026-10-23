using FluentMigrator;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.Events;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Data.Migrations;

[Migration(014)]
public class M008_3 : MigrationBase {
    public override void Up() {
        Alter.Table(SignatureCampaign.TableName)
            .AddColumn(nameof(SignatureCampaign.Visibility)).AsInt32().NotNullable().WithDefaultValue((int)SignatureCampaignVisibility.Private)
            .AddColumn(nameof(SignatureCampaign.PostalAddress)).AsString(SignatureCampaign.PostalAddressMaxLength).Nullable()
            .AddColumn(nameof(SignatureCampaign.ContactEmail)).AsString(SignatureCampaign.ContactEmailMaxLength).Nullable();

        Execute.Sql($@"
            UPDATE `{SignatureCampaign.TableName}` SET `{nameof(SignatureCampaign.Visibility)}` = {(int)SignatureCampaignVisibility.MembersOnly}
            WHERE `{M008.VisibleToMembersColumn}` = 1");

        Delete.Column(M008.VisibleToMembersColumn).FromTable(SignatureCampaign.TableName);

        Alter.Table(Event.TableName)
            .AddColumn(nameof(Event.StartTime)).AsCustom("TIME").Nullable()
            .AddColumn(nameof(Event.MeetingUrl)).AsString(Event.MeetingUrlMaxLength).Nullable();
    }

    public override void Down() {
        Delete.Column(nameof(Event.StartTime)).FromTable(Event.TableName);
        Delete.Column(nameof(Event.MeetingUrl)).FromTable(Event.TableName);

        Alter.Table(SignatureCampaign.TableName)
            .AddColumn(M008.VisibleToMembersColumn).AsBoolean().NotNullable().WithDefaultValue(false);

        Execute.Sql($@"
            UPDATE `{SignatureCampaign.TableName}` SET `{M008.VisibleToMembersColumn}` = 1
            WHERE `{nameof(SignatureCampaign.Visibility)}` <> {(int)SignatureCampaignVisibility.Private}");

        Delete.Column(nameof(SignatureCampaign.Visibility)).FromTable(SignatureCampaign.TableName);
        Delete.Column(nameof(SignatureCampaign.PostalAddress)).FromTable(SignatureCampaign.TableName);
        Delete.Column(nameof(SignatureCampaign.ContactEmail)).FromTable(SignatureCampaign.TableName);
    }
}
