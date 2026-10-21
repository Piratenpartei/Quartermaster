using System;
using System.Collections.Generic;

namespace Quartermaster.Api.SupportSignatures;

public class CertificationOfficeDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public List<OfficeMunicipalityDTO> Municipalities { get; set; } = [];
    public bool HasShipments { get; set; }
}

public class OfficeMunicipalityDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class CertificationOfficeSaveRequest {
    /// <summary>The office to update; bound from the route, unused on create.</summary>
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
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

/// <summary>
/// Where forms of a municipality should go: an existing office, or a new one to create (for a Samtgemeinde member with all
/// its members that have no office yet).
/// </summary>
public class OfficeSuggestionDTO {
    public Guid MunicipalityId { get; set; }
    public Guid? ExistingOfficeId { get; set; }
    public string SuggestedName { get; set; } = "";
    public List<OfficeMunicipalityDTO> Municipalities { get; set; } = [];
}
