using FluentMigrator;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Data.Migrations;

[Migration(013)]
public class M008 : MigrationBase {
    public override void Up() {
        Create.Table(MunicipalityAssociation.TableName)
            .WithColumn(nameof(MunicipalityAssociation.Key)).AsFixedLengthString(MunicipalityAssociation.KeyLength).PrimaryKey()
            .WithColumn(nameof(MunicipalityAssociation.Name)).AsString(MunicipalityAssociation.NameMaxLength).NotNullable()
            .WithColumn(nameof(MunicipalityAssociation.Kind)).AsInt32().NotNullable()
            .WithColumn(nameof(MunicipalityAssociation.ImportedAt)).AsCustom("DATETIME(6)").NotNullable();

        Alter.Table(AdministrativeDivision.TableName)
            .AddColumn(nameof(AdministrativeDivision.MunicipalityAssociationKey)).AsFixedLengthString(MunicipalityAssociation.KeyLength).Nullable();

        Create.Table(SignatureCampaign.TableName)
            .WithColumn(nameof(SignatureCampaign.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SignatureCampaign.ChapterId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureCampaign.Name)).AsString(SignatureCampaign.NameMaxLength).NotNullable()
            .WithColumn(nameof(SignatureCampaign.Target)).AsInt32().NotNullable()
            .WithColumn(nameof(SignatureCampaign.MarginPercent)).AsInt32().NotNullable()
            .WithColumn(nameof(SignatureCampaign.Deadline)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(SignatureCampaign.OverdueDays)).AsInt32().NotNullable()
            .WithColumn(nameof(SignatureCampaign.VisibleToMembers)).AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn(nameof(SignatureCampaign.MemberNotice)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(SignatureCampaign.HasForm)).AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn(nameof(SignatureCampaign.FormFileName)).AsString(SignatureCampaign.FormFileNameMaxLength).Nullable()
            .WithColumn(nameof(SignatureCampaign.ArchivedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(SignatureCampaign.CreatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_SignatureCampaigns_ChapterId_Chapters_Id")
            .FromTable(SignatureCampaign.TableName).ForeignColumn(nameof(SignatureCampaign.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(SignatureIntake.TableName)
            .WithColumn(nameof(SignatureIntake.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SignatureIntake.CampaignId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureIntake.AdministrativeDivisionId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureIntake.Count)).AsInt32().NotNullable()
            .WithColumn(nameof(SignatureIntake.AlreadyCertified)).AsBoolean().NotNullable()
            .WithColumn(nameof(SignatureIntake.ReceivedOn)).AsDate().NotNullable()
            .WithColumn(nameof(SignatureIntake.Note)).AsString(SignatureIntake.NoteMaxLength).Nullable()
            .WithColumn(nameof(SignatureIntake.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(SignatureIntake.CreatedByUserId)).AsGuid().NotNullable();

        Create.ForeignKey("FK_SignatureIntakes_CampaignId")
            .FromTable(SignatureIntake.TableName).ForeignColumn(nameof(SignatureIntake.CampaignId))
            .ToTable(SignatureCampaign.TableName).PrimaryColumn(nameof(SignatureCampaign.Id));
        Create.ForeignKey("FK_SignatureIntakes_DivisionId")
            .FromTable(SignatureIntake.TableName).ForeignColumn(nameof(SignatureIntake.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));
        Create.Index("IX_SignatureIntakes_CampaignId_DivisionId")
            .OnTable(SignatureIntake.TableName)
            .OnColumn(nameof(SignatureIntake.CampaignId)).Ascending()
            .OnColumn(nameof(SignatureIntake.AdministrativeDivisionId)).Ascending();

        Create.Table(CertificationOffice.TableName)
            .WithColumn(nameof(CertificationOffice.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(CertificationOffice.Name)).AsString(CertificationOffice.NameMaxLength).NotNullable()
            .WithColumn(nameof(CertificationOffice.Street)).AsString(CertificationOffice.AddressFieldMaxLength).NotNullable()
            .WithColumn(nameof(CertificationOffice.PostCode)).AsString(CertificationOffice.AddressFieldMaxLength).NotNullable()
            .WithColumn(nameof(CertificationOffice.City)).AsString(CertificationOffice.AddressFieldMaxLength).NotNullable()
            .WithColumn(nameof(CertificationOffice.Email)).AsString(CertificationOffice.EmailMaxLength).Nullable()
            .WithColumn(nameof(CertificationOffice.Phone)).AsString(CertificationOffice.PhoneMaxLength).Nullable()
            .WithColumn(nameof(CertificationOffice.Notes)).AsString(CertificationOffice.NotesMaxLength).Nullable();

        Create.Table(CertificationOfficeMunicipality.TableName)
            .WithColumn(nameof(CertificationOfficeMunicipality.AdministrativeDivisionId)).AsGuid().PrimaryKey()
            .WithColumn(nameof(CertificationOfficeMunicipality.OfficeId)).AsGuid().NotNullable();

        Create.ForeignKey("FK_CertificationOfficeMunicipalities_OfficeId")
            .FromTable(CertificationOfficeMunicipality.TableName).ForeignColumn(nameof(CertificationOfficeMunicipality.OfficeId))
            .ToTable(CertificationOffice.TableName).PrimaryColumn(nameof(CertificationOffice.Id));
        Create.ForeignKey("FK_CertificationOfficeMunicipalities_DivisionId")
            .FromTable(CertificationOfficeMunicipality.TableName).ForeignColumn(nameof(CertificationOfficeMunicipality.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        Create.Table(SignatureShipment.TableName)
            .WithColumn(nameof(SignatureShipment.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SignatureShipment.CampaignId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureShipment.OfficeId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureShipment.SentOn)).AsDate().Nullable()
            .WithColumn(nameof(SignatureShipment.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(SignatureShipment.CreatedByUserId)).AsGuid().NotNullable();

        Create.ForeignKey("FK_SignatureShipments_CampaignId")
            .FromTable(SignatureShipment.TableName).ForeignColumn(nameof(SignatureShipment.CampaignId))
            .ToTable(SignatureCampaign.TableName).PrimaryColumn(nameof(SignatureCampaign.Id));
        Create.ForeignKey("FK_SignatureShipments_OfficeId")
            .FromTable(SignatureShipment.TableName).ForeignColumn(nameof(SignatureShipment.OfficeId))
            .ToTable(CertificationOffice.TableName).PrimaryColumn(nameof(CertificationOffice.Id));

        Create.Table(SignatureShipmentLine.TableName)
            .WithColumn(nameof(SignatureShipmentLine.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SignatureShipmentLine.ShipmentId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureShipmentLine.AdministrativeDivisionId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureShipmentLine.Count)).AsInt32().NotNullable();

        Create.ForeignKey("FK_SignatureShipmentLines_ShipmentId")
            .FromTable(SignatureShipmentLine.TableName).ForeignColumn(nameof(SignatureShipmentLine.ShipmentId))
            .ToTable(SignatureShipment.TableName).PrimaryColumn(nameof(SignatureShipment.Id));
        Create.ForeignKey("FK_SignatureShipmentLines_DivisionId")
            .FromTable(SignatureShipmentLine.TableName).ForeignColumn(nameof(SignatureShipmentLine.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));
        Create.Index("UX_SignatureShipmentLines_ShipmentId_DivisionId")
            .OnTable(SignatureShipmentLine.TableName)
            .OnColumn(nameof(SignatureShipmentLine.ShipmentId)).Ascending()
            .OnColumn(nameof(SignatureShipmentLine.AdministrativeDivisionId)).Ascending()
            .WithOptions().Unique();

        Create.Table(SignatureReturn.TableName)
            .WithColumn(nameof(SignatureReturn.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SignatureReturn.ShipmentId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureReturn.ReturnedOn)).AsDate().NotNullable()
            .WithColumn(nameof(SignatureReturn.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(SignatureReturn.CreatedByUserId)).AsGuid().NotNullable();

        Create.ForeignKey("FK_SignatureReturns_ShipmentId")
            .FromTable(SignatureReturn.TableName).ForeignColumn(nameof(SignatureReturn.ShipmentId))
            .ToTable(SignatureShipment.TableName).PrimaryColumn(nameof(SignatureShipment.Id));

        Create.Table(SignatureReturnLine.TableName)
            .WithColumn(nameof(SignatureReturnLine.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SignatureReturnLine.ReturnId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureReturnLine.AdministrativeDivisionId)).AsGuid().NotNullable()
            .WithColumn(nameof(SignatureReturnLine.Certified)).AsInt32().NotNullable()
            .WithColumn(nameof(SignatureReturnLine.Rejected)).AsInt32().NotNullable()
            .WithColumn(nameof(SignatureReturnLine.RejectionReason)).AsInt32().Nullable();

        Create.ForeignKey("FK_SignatureReturnLines_ReturnId")
            .FromTable(SignatureReturnLine.TableName).ForeignColumn(nameof(SignatureReturnLine.ReturnId))
            .ToTable(SignatureReturn.TableName).PrimaryColumn(nameof(SignatureReturn.Id));
        Create.ForeignKey("FK_SignatureReturnLines_DivisionId")
            .FromTable(SignatureReturnLine.TableName).ForeignColumn(nameof(SignatureReturnLine.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));
    }

    public override void Down() {
        Delete.Table(SignatureReturnLine.TableName);
        Delete.Table(SignatureReturn.TableName);
        Delete.Table(SignatureShipmentLine.TableName);
        Delete.Table(SignatureShipment.TableName);
        Delete.Table(CertificationOfficeMunicipality.TableName);
        Delete.Table(CertificationOffice.TableName);
        Delete.Table(SignatureIntake.TableName);
        Delete.Table(SignatureCampaign.TableName);
        Delete.Column(nameof(AdministrativeDivision.MunicipalityAssociationKey)).FromTable(AdministrativeDivision.TableName);
        Delete.Table(MunicipalityAssociation.TableName);
    }
}
