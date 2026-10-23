using FluentMigrator;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Data.Templates;

namespace Quartermaster.Data.Migrations;

[Migration(015)]
public class M008_4 : MigrationBase {
    public const string CoverLetterIdentifier = "templates.support_signatures.cover_letter";
    public const string RecipientNameTag = "{% envelope_recipient_name letter.Office.Name %}";
    public const string RecipientAdditionTag = "{% envelope_recipient_addition letter.Office.Addition %}";

    public override void Up() {
        Alter.Table(CertificationOffice.TableName)
            .AddColumn(nameof(CertificationOffice.AddressAddition)).AsString(CertificationOffice.AddressFieldMaxLength).Nullable()
            .AddColumn(nameof(CertificationOffice.OfficialKey)).AsString(CertificationOffice.OfficialKeyMaxLength).Nullable();

        Create.Index("UX_CertificationOffices_OfficialKey")
            .OnTable(CertificationOffice.TableName)
            .OnColumn(nameof(CertificationOffice.OfficialKey)).Ascending()
            .WithOptions().Unique();

        Create.Table(OfficialOfficeMunicipality.TableName)
            .WithColumn(nameof(OfficialOfficeMunicipality.AdministrativeDivisionId)).AsGuid().PrimaryKey()
            .WithColumn(nameof(OfficialOfficeMunicipality.OfficeId)).AsGuid().NotNullable();

        Create.ForeignKey("FK_OfficialOfficeMunicipalities_OfficeId")
            .FromTable(OfficialOfficeMunicipality.TableName).ForeignColumn(nameof(OfficialOfficeMunicipality.OfficeId))
            .ToTable(CertificationOffice.TableName).PrimaryColumn(nameof(CertificationOffice.Id));
        Create.ForeignKey("FK_OfficialOfficeMunicipalities_DivisionId")
            .FromTable(OfficialOfficeMunicipality.TableName).ForeignColumn(nameof(OfficialOfficeMunicipality.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        // Seeds are only inserted once, so the system template and the chapter copies get the new address line here.
        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(`{nameof(Template.Body)}`, '{RecipientNameTag}', '{RecipientNameTag}{RecipientAdditionTag}')
            WHERE `{nameof(Template.Identifier)}` = '{CoverLetterIdentifier}' AND `{nameof(Template.Body)}` NOT LIKE '%envelope_recipient_addition%'");
    }

    public override void Down() {
        Execute.Sql($@"
            UPDATE `{Template.TableName}` SET `{nameof(Template.Body)}` = REPLACE(`{nameof(Template.Body)}`, '{RecipientAdditionTag}', '')
            WHERE `{nameof(Template.Identifier)}` = '{CoverLetterIdentifier}'");

        Delete.Table(OfficialOfficeMunicipality.TableName);
        Delete.Index("UX_CertificationOffices_OfficialKey").OnTable(CertificationOffice.TableName);
        Delete.Column(nameof(CertificationOffice.AddressAddition)).FromTable(CertificationOffice.TableName);
        Delete.Column(nameof(CertificationOffice.OfficialKey)).FromTable(CertificationOffice.TableName);
    }
}
