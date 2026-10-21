namespace Quartermaster.Server.SupportSignatures;

/// <param name="Prepared">Forms in shipments that are prepared but not yet confirmed as sent; they still count as waiting.</param>
public record MunicipalityTally(int Received, int AlreadyCertified, int Sent, int Prepared, int Certified, int Rejected) {
    public int Waiting => Received - AlreadyCertified - Sent;

    /// <summary>Waiting forms not yet in a prepared shipment; only these can go into a new one.</summary>
    public int Plannable => Waiting - Prepared;
    public int Open => Sent - Certified - Rejected;
    public int CertifiedTotal => AlreadyCertified + Certified;
}
