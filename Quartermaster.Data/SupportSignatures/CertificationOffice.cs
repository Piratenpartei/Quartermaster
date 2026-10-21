using System;
using System.Collections.Generic;
using LinqToDB.Mapping;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>The office that certifies the voting rights for one or more municipalities, e.g. a Samtgemeinde administration.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class CertificationOffice {
    public const string TableName = "CertificationOffices";
    public const int NameMaxLength = 200;
    public const int AddressFieldMaxLength = 200;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 64;
    public const int NotesMaxLength = 1000;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }

    public CertificationOfficeDTO ToDto(List<OfficeMunicipalityDTO> municipalities, bool hasShipments) {
        return new() {
            Id = Id,
            Name = Name,
            Street = Street,
            PostCode = PostCode,
            City = City,
            Email = Email,
            Phone = Phone,
            Notes = Notes,
            Municipalities = municipalities,
            HasShipments = hasShipments
        };
    }
}
