using System;
using System.Collections.Generic;

namespace Quartermaster.Api.SupportSignatures;

public class SignatureOverviewDTO {
    public SignatureTotalsDTO Totals { get; set; } = new();
    public List<RejectionCountDTO> Rejections { get; set; } = [];

    /// <summary>One row per Landkreis (or kreisfreie Stadt) with its municipalities as children.</summary>
    public List<SignatureRegionRowDTO> Regions { get; set; } = [];

    /// <summary>Whole days until the deadline; negative once it has passed.</summary>
    public int DaysLeft { get; set; }
}

public class RejectionCountDTO {
    public SignatureRejectionReason? Reason { get; set; }
    public int Count { get; set; }
}

public class SignatureRegionRowDTO {
    public Guid DivisionId { get; set; }
    public string Name { get; set; } = "";
    public int Received { get; set; }
    public int Certified { get; set; }
    public int Rejected { get; set; }
    public List<SignatureRegionRowDTO> Children { get; set; } = [];
}
