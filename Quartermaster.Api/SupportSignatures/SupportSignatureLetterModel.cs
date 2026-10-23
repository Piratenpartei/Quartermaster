using System.Collections.Generic;

namespace Quartermaster.Api.SupportSignatures;

/// <summary>The cover letter of one shipment, available to its template as <c>letter</c>.</summary>
public class SupportSignatureLetterModel {
    public SupportSignatureLetterCampaign Campaign { get; set; } = new();
    public SupportSignatureLetterOffice Office { get; set; } = new();
    public List<SupportSignatureLetterLine> Lines { get; set; } = [];
    public int Total { get; set; }

    /// <summary>The date the letter is printed, as dd.MM.yyyy.</summary>
    public string Date { get; set; } = "";
}

public class SupportSignatureLetterCampaign {
    public string Name { get; set; } = "";

    /// <summary>The last day forms count, as dd.MM.yyyy.</summary>
    public string Deadline { get; set; } = "";
}

public class SupportSignatureLetterOffice {
    public string Name { get; set; } = "";

    /// <summary>A line under the name such as "Wahlamt"; empty when the office has none.</summary>
    public string Addition { get; set; } = "";

    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
}

public class SupportSignatureLetterLine {
    public string Municipality { get; set; } = "";
    public int Count { get; set; }
}
