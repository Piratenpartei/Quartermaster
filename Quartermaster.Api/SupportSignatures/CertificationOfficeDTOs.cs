using System;
using System.Collections.Generic;

namespace Quartermaster.Api.SupportSignatures;

public class CertificationOfficeDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? AddressAddition { get; set; }
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public List<OfficeMunicipalityDTO> Municipalities { get; set; } = [];
    public bool HasShipments { get; set; }

    /// <summary>From the official address directory; read-only, a hand-entered office overrides it.</summary>
    public bool IsOfficial { get; set; }
}

public class OfficeMunicipalityDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class CertificationOfficeSaveRequest {
    /// <summary>The office to update; bound from the route, unused on create.</summary>
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? AddressAddition { get; set; }
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public List<Guid> MunicipalityIds { get; set; } = [];
}

public class CertificationOfficeIdRequest {
    public Guid Id { get; set; }
}

public class OfficeSuggestionRequest {
    public Guid MunicipalityId { get; set; }
}

public class OfficeResponsibleRequest {
    public Guid MunicipalityId { get; set; }
}

/// <summary>
/// Where forms of a municipality should go: an existing hand-entered office, or a new one to create (for a Samtgemeinde member
/// with all its members that have no office yet), prefilled from the official address directory when it knows the municipality.
/// </summary>
public class OfficeSuggestionDTO {
    public Guid MunicipalityId { get; set; }
    public Guid? ExistingOfficeId { get; set; }
    public string SuggestedName { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public bool FromOfficialDirectory { get; set; }
    public List<OfficeMunicipalityDTO> Municipalities { get; set; } = [];
}

public class OfficeImportRequest {
    public const int MaxContentBytes = 2 * 1024 * 1024;

    public byte[] Content { get; set; } = [];

    /// <summary>False only previews what the import would change.</summary>
    public bool Apply { get; set; }
}

public class OfficeImportResultDTO {
    public bool Applied { get; set; }
    public int Rows { get; set; }
    public int SameAsOfficial { get; set; }

    /// <summary>Hand-entered assignments the file replaces with the official address.</summary>
    public int RemovedAssignments { get; set; }

    public List<OfficeImportEntryDTO> Entries { get; set; } = [];
    public List<string> UnknownKeys { get; set; } = [];
    public List<OfficeImportProblemDTO> Problems { get; set; } = [];
}

public class OfficeImportEntryDTO {
    public string Name { get; set; } = "";
    public string? AddressAddition { get; set; }
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public bool IsNew { get; set; }
    public List<string> Municipalities { get; set; } = [];
}

public class OfficeImportProblemDTO {
    public int Line { get; set; }
    public string ErrorKey { get; set; } = "";
}
