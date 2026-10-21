using System;
using System.Collections.Generic;

namespace Quartermaster.Api.SupportSignatures;

public class ShipmentPlanDTO {
    public List<ShipmentPlanOfficeDTO> Offices { get; set; } = [];

    /// <summary>Municipalities with waiting forms but no office yet.</summary>
    public List<PlanLineDTO> Unassigned { get; set; } = [];
}

public class ShipmentPlanOfficeDTO {
    public Guid OfficeId { get; set; }
    public string OfficeName { get; set; } = "";
    public List<PlanLineDTO> Lines { get; set; } = [];
}

public class PlanLineDTO {
    public Guid MunicipalityId { get; set; }
    public string MunicipalityName { get; set; } = "";
    public int Count { get; set; }
}

public class ShipmentPrepareRequest {
    /// <summary>The campaign; bound from the route.</summary>
    public Guid Id { get; set; }
    public List<Guid> OfficeIds { get; set; } = [];
}

public class ShipmentPrepareResponse {
    public List<Guid> ShipmentIds { get; set; } = [];
}

public class SingleShipmentRequest {
    /// <summary>The campaign; bound from the route.</summary>
    public Guid Id { get; set; }
    public Guid OfficeId { get; set; }
    public List<PlanLineInput> Lines { get; set; } = [];
}

public class PlanLineInput {
    public Guid MunicipalityId { get; set; }
    public int Count { get; set; }
}

public class ShipmentIdRequest {
    public Guid Id { get; set; }
}

public class ShipmentConfirmRequest {
    public Guid Id { get; set; }
    public DateOnly SentOn { get; set; }
}

public class ReturnSaveRequest {
    /// <summary>The shipment when recording, the return when updating; bound from the route.</summary>
    public Guid Id { get; set; }
    public DateOnly ReturnedOn { get; set; }
    public List<ReturnLineInput> Lines { get; set; } = [];
}

public class ReturnLineInput {
    public Guid MunicipalityId { get; set; }
    public int Certified { get; set; }
    public int Rejected { get; set; }
    public SignatureRejectionReason? RejectionReason { get; set; }
}

public class ReturnIdRequest {
    public Guid Id { get; set; }
}

public class SignatureShipmentDTO {
    public Guid Id { get; set; }
    public Guid OfficeId { get; set; }
    public string OfficeName { get; set; } = "";

    /// <summary>Null while the shipment is prepared but not yet confirmed as sent.</summary>
    public DateOnly? SentOn { get; set; }
    public List<ShipmentLineDTO> Lines { get; set; } = [];
    public List<SignatureReturnDTO> Returns { get; set; } = [];
    public int Open { get; set; }
    public bool IsOverdue { get; set; }
}

public class ShipmentLineDTO {
    public Guid MunicipalityId { get; set; }
    public string MunicipalityName { get; set; } = "";
    public int Sent { get; set; }
    public int Certified { get; set; }
    public int Rejected { get; set; }
    public int Open { get; set; }
}

public class SignatureReturnDTO {
    public Guid Id { get; set; }
    public DateOnly ReturnedOn { get; set; }
    public int Certified { get; set; }
    public int Rejected { get; set; }
    public List<ReturnLineInput> Lines { get; set; } = [];
}
